using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.Logging.Abstractions;

namespace BaristaNotes.Tests.Unit;

public sealed class PhotoWorkflowTests
{
    [Fact]
    public async Task AutomaticCoffee_UsesClassificationDetailsWithoutExtraction()
    {
        var details = new BeanLabelExtraction
        {
            Success = true,
            Name = "North Star",
            Roaster = "Contoso"
        };
        var host = new TestHost(Photo([1, 2, 3]));
        var vision = new TestVision
        {
            Classification = new PhotoWorkflowAnalysis
            {
                Success = true,
                IsObvious = true,
                Intent = PhotoWorkflowIntent.Coffee,
                CoffeeDetails = details
            }
        };

        using var workflow = Create(host, vision);
        await workflow.RunAsync(CancellationToken.None);

        Assert.Same(details, host.CoffeePrefill);
        Assert.Equal(0, host.ChoiceCalls);
        Assert.Equal(0, vision.ExtractionCalls);
    }

    [Fact]
    public async Task UncertainPhoto_UsesManualChoice()
    {
        var host = new TestHost(Photo([4, 5, 6]))
        {
            Choice = PhotoIntentChoice.Profile
        };
        var vision = new TestVision
        {
            Classification = new PhotoWorkflowAnalysis
            {
                Success = true,
                Intent = PhotoWorkflowIntent.Unknown
            }
        };

        using var workflow = Create(host, vision);
        await workflow.RunAsync(CancellationToken.None);

        Assert.Equal(1, host.ChoiceCalls);
        Assert.Equal([4, 5, 6], host.ProfileImage);
    }

    [Fact]
    public async Task Retake_CapturesAgainAndUsesSecondResult()
    {
        var host = new TestHost(Photo([1]), Photo([2]))
        {
            Choice = PhotoIntentChoice.Retake
        };
        var vision = new TestVision
        {
            Classifications =
            [
                new PhotoWorkflowAnalysis
                {
                    Success = true,
                    Intent = PhotoWorkflowIntent.Unknown
                },
                new PhotoWorkflowAnalysis
                {
                    Success = true,
                    IsObvious = true,
                    Intent = PhotoWorkflowIntent.Profile
                }
            ]
        };

        using var workflow = Create(host, vision);
        await workflow.RunAsync(CancellationToken.None);

        Assert.Equal(2, host.CaptureCalls);
        Assert.Equal([2], host.ProfileImage);
    }

    [Fact]
    public async Task CoffeeWithoutDetails_ExtractsEditablePrefill()
    {
        var extraction = new BeanLabelExtraction
        {
            Success = true,
            Name = "Extracted Coffee"
        };
        var host = new TestHost(Photo([7]));
        var vision = new TestVision
        {
            Classification = new PhotoWorkflowAnalysis
            {
                Success = true,
                IsObvious = true,
                Intent = PhotoWorkflowIntent.Coffee
            },
            Extraction = extraction
        };

        using var workflow = Create(host, vision);
        await workflow.RunAsync(CancellationToken.None);

        Assert.Equal(1, vision.ExtractionCalls);
        Assert.Same(extraction, host.CoffeePrefill);
    }

    [Fact]
    public async Task ClassificationFailure_ShowsErrorThenOffersIntentForTheSamePhoto()
    {
        var host = new TestHost(Photo([7])) { Choice = PhotoIntentChoice.Profile };
        var vision = new TestVision
        {
            Classification = PhotoWorkflowAnalysis.Error("Vision service is not configured.")
        };

        using var workflow = Create(host, vision);
        await workflow.RunAsync(CancellationToken.None);

        Assert.Equal(1, host.ChoiceCalls);
        Assert.Equal(1, host.CaptureCalls);
        Assert.Equal([7], host.ProfileImage);
        Assert.Null(host.CoffeePrefill);
        var alert = Assert.Single(host.Alerts);
        Assert.Equal("Photo Analysis Unavailable", alert.Title);
        Assert.Equal("Vision service is not configured.", alert.Message);
        Assert.Equal(["capture", "alert", "choice", "profile"], host.Events);
        Assert.False(host.ProcessingStates.Last());
        Assert.False(workflow.IsActive);
    }

    [Fact]
    public async Task ClassificationFailure_RetakeUsesANewCapture()
    {
        var host = new TestHost(Photo([1]), Photo([2])) { Choice = PhotoIntentChoice.Retake };
        var vision = new TestVision
        {
            Classifications =
            [
                PhotoWorkflowAnalysis.Error("Provider unavailable."),
                new PhotoWorkflowAnalysis
                {
                    Success = true, IsObvious = true, Intent = PhotoWorkflowIntent.Profile
                }
            ]
        };

        using var workflow = Create(host, vision);
        await workflow.RunAsync(CancellationToken.None);

        Assert.Equal(2, host.CaptureCalls);
        Assert.Equal(1, host.ChoiceCalls);
        Assert.Equal([2], host.ProfileImage);
        Assert.Single(host.Alerts);
        Assert.Equal(["capture", "alert", "choice", "capture", "profile"], host.Events);
        Assert.False(host.ProcessingStates.Last());
    }

    [Fact]
    public async Task ClassificationFailure_WaitsForErrorAcknowledgementBeforeIntentChoice()
    {
        var dismissed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var host = new TestHost(Photo([13]))
        {
            Choice = PhotoIntentChoice.Profile,
            AlertCompletion = dismissed.Task
        };
        var vision = new TestVision { Classification = PhotoWorkflowAnalysis.Error("Unavailable.") };

        using var workflow = Create(host, vision);
        var run = workflow.RunAsync(CancellationToken.None);

        Assert.False(run.IsCompleted);
        Assert.Equal(0, host.ChoiceCalls);
        Assert.Null(host.ProfileImage);
        dismissed.SetResult();
        await run;

        Assert.Equal(1, host.ChoiceCalls);
        Assert.Equal([13], host.ProfileImage);
        Assert.Equal(["capture", "alert", "choice", "profile"], host.Events);
    }

    [Fact]
    public async Task ClassificationFailure_ManualCancelDoesNotRouteOrRetake()
    {
        var host = new TestHost(Photo([3])) { Choice = PhotoIntentChoice.Cancel };
        var vision = new TestVision { Classification = PhotoWorkflowAnalysis.Error("Unavailable.") };

        using var workflow = Create(host, vision);
        await workflow.RunAsync(CancellationToken.None);

        Assert.Equal(1, host.ChoiceCalls);
        Assert.Equal(1, host.CaptureCalls);
        Assert.Null(host.ProfileImage);
        Assert.Null(host.CoffeePrefill);
        Assert.Null(vision.RoomQuestion);
        Assert.False(host.ProcessingStates.Last());
        Assert.False(workflow.IsActive);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ClassificationFailure_CancellationOrOwnerDepartureStopsRecovery(
        bool duringChoice, bool ownerDeparture)
    {
        using var cancellation = new CancellationTokenSource();
        var host = new TestHost(Photo([4])) { Choice = PhotoIntentChoice.Profile };
        void Leave()
        {
            if (ownerDeparture) host.Current = false;
            else cancellation.Cancel();
        }
        if (duringChoice) host.Choosing = Leave;
        else host.Alerted = Leave;
        var vision = new TestVision { Classification = PhotoWorkflowAnalysis.Error("Unavailable.") };

        using var workflow = Create(host, vision);
        await workflow.RunAsync(cancellation.Token);

        Assert.Equal(duringChoice ? 1 : 0, host.ChoiceCalls);
        Assert.Null(host.ProfileImage);
        Assert.Null(host.CoffeePrefill);
        Assert.Single(host.Alerts);
        Assert.False(host.ProcessingStates.Last());
        Assert.False(workflow.IsActive);
    }

    [Fact]
    public async Task FailedCoffeeExtraction_ShowsErrorAndOpensBlankEditableForm()
    {
        var host = new TestHost(Photo([7]))
        {
            Choice = PhotoIntentChoice.Coffee
        };
        var vision = new TestVision
        {
            Classification = new PhotoWorkflowAnalysis
            {
                Success = true,
                Intent = PhotoWorkflowIntent.Unknown
            },
            Extraction = new BeanLabelExtraction
            {
                Success = false,
                ErrorMessage = "Extraction failed."
            }
        };

        using var workflow = Create(host, vision);
        await workflow.RunAsync(CancellationToken.None);

        Assert.Equal(1, vision.ExtractionCalls);
        var alert = Assert.Single(host.Alerts);
        Assert.Equal("Couldn't Read Label", alert.Title);
        Assert.True(host.CoffeePrefill?.Success);
        Assert.Null(host.CoffeePrefill?.Name);
    }

    [Fact]
    public async Task RoomAnalysis_ShowsSharedSuccessMessage()
    {
        var host = new TestHost(Photo([8]));
        var vision = new TestVision
        {
            Classification = new PhotoWorkflowAnalysis
            {
                Success = true,
                IsObvious = true,
                Intent = PhotoWorkflowIntent.Room
            },
            RoomResult = new VisionAnalysisResult
            {
                Success = true,
                PeopleCount = 3,
                CupsNeeded = 3,
                BeansNeededGrams = 54
            }
        };

        using var workflow = Create(host, vision);
        await workflow.RunAsync(CancellationToken.None);

        var alert = Assert.Single(host.Alerts);
        Assert.Equal("Analysis Complete", alert.Title);
        Assert.Equal(
            "I see 3 people. You need 3 cups of coffee, which requires about 54g of beans.",
            alert.Message);
        Assert.Equal(PhotoWorkflow.RoomQuestion, vision.RoomQuestion);
    }

    [Fact]
    public async Task OwnerReplacement_CancelsBeforeNavigationOrFeedback()
    {
        var host = new TestHost(Photo([9]));
        var vision = new TestVision
        {
            ClassificationCallback = () =>
            {
                host.Current = false;
                return new PhotoWorkflowAnalysis
                {
                    Success = true,
                    IsObvious = true,
                    Intent = PhotoWorkflowIntent.Profile
                };
            }
        };

        using var workflow = Create(host, vision);
        await workflow.RunAsync(CancellationToken.None);

        Assert.Null(host.ProfileImage);
        Assert.Empty(host.Alerts);
        Assert.False(workflow.IsActive);
    }

    [Fact]
    public async Task ClassificationException_ShowsErrorThenOffersSamePhotoRecovery()
    {
        var host = new TestHost(Photo([10])) { Choice = PhotoIntentChoice.Profile };
        var vision = new TestVision
        {
            ClassificationError = new InvalidOperationException("model unavailable")
        };

        using var workflow = Create(host, vision);
        await workflow.RunAsync(CancellationToken.None);

        var alert = Assert.Single(host.Alerts);
        Assert.Equal("Photo Analysis Unavailable", alert.Title);
        Assert.Contains("model unavailable", alert.Message);
        Assert.Equal(1, host.ChoiceCalls);
        Assert.Equal([10], host.ProfileImage);
        Assert.False(host.ProcessingStates.Last());
        Assert.False(workflow.IsActive);
    }

    [Fact]
    public async Task ClassificationCancellation_DoesNotOfferRecovery()
    {
        using var cancellation = new CancellationTokenSource();
        var host = new TestHost(Photo([11])) { Choice = PhotoIntentChoice.Profile };
        var vision = new TestVision
        {
            ClassificationCallback = () =>
            {
                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            }
        };

        using var workflow = Create(host, vision);
        await workflow.RunAsync(cancellation.Token);

        Assert.Empty(host.Alerts);
        Assert.Equal(0, host.ChoiceCalls);
        Assert.Null(host.ProfileImage);
        Assert.False(host.ProcessingStates.Last());
        Assert.False(workflow.IsActive);
    }

    [Fact]
    public async Task ProviderTimeoutWithoutOwnerCancellation_OffersSamePhotoRecovery()
    {
        var host = new TestHost(Photo([12])) { Choice = PhotoIntentChoice.Profile };
        var vision = new TestVision
        {
            ClassificationError = new OperationCanceledException("Provider timed out.")
        };

        using var workflow = Create(host, vision);
        await workflow.RunAsync(CancellationToken.None);

        Assert.Equal("Provider timed out.", Assert.Single(host.Alerts).Message);
        Assert.Equal(1, host.ChoiceCalls);
        Assert.Equal([12], host.ProfileImage);
        Assert.False(host.ProcessingStates.Last());
    }

    [Fact]
    public async Task CaptureReadFailure_DoesNotOfferIntentWithoutImageBytes()
    {
        var host = new TestHost(new VoicePhoto("photo.jpg",
            () => Task.FromException<Stream>(new IOException("Capture could not be read."))));
        var vision = new TestVision();

        using var workflow = Create(host, vision);
        await workflow.RunAsync(CancellationToken.None);

        var alert = Assert.Single(host.Alerts);
        Assert.Equal("Error", alert.Title);
        Assert.Contains("Capture could not be read.", alert.Message);
        Assert.Equal(0, host.ChoiceCalls);
        Assert.Null(host.ProfileImage);
        Assert.False(host.ProcessingStates.Last());
        Assert.False(workflow.IsActive);
    }

    private static PhotoWorkflow Create(TestHost host, TestVision vision) =>
        new(host, vision, NullLogger.Instance);

    private static VoicePhoto Photo(byte[] bytes) =>
        new("photo.jpg", () => Task.FromResult<Stream>(new MemoryStream(bytes)));

    private sealed class TestHost(params VoicePhoto[] photos) : IPhotoWorkflowHost
    {
        private readonly Queue<VoicePhoto> _photos = new(photos);

        public bool Current { get; set; } = true;
        public bool CaptureSupported { get; set; } = true;
        public PhotoIntentChoice Choice { get; set; } = PhotoIntentChoice.Cancel;
        public int CaptureCalls { get; private set; }
        public int ChoiceCalls { get; private set; }
        public List<bool> ProcessingStates { get; } = [];
        public BeanLabelExtraction? CoffeePrefill { get; private set; }
        public byte[]? ProfileImage { get; private set; }
        public List<(string Title, string Message)> Alerts { get; } = [];
        public List<string> Events { get; } = [];
        public Action? Alerted { get; set; }
        public Action? Choosing { get; set; }
        public Task AlertCompletion { get; set; } = Task.CompletedTask;
        public bool IsCurrent => Current;
        public bool IsCaptureSupported => CaptureSupported;

        public Task<VoicePhoto?> CaptureAsync(CancellationToken cancellation)
        {
            CaptureCalls++;
            Events.Add("capture");
            return Task.FromResult<VoicePhoto?>(
                _photos.TryDequeue(out var photo) ? photo : null);
        }

        public void SetProcessing(bool processing) => ProcessingStates.Add(processing);

        public Task<PhotoIntentChoice> ChooseIntentAsync(CancellationToken cancellation)
        {
            ChoiceCalls++;
            Events.Add("choice");
            Choosing?.Invoke();
            return Task.FromResult(Choice);
        }

        public Task OpenCoffeeAsync(
            BeanLabelExtraction prefill,
            CancellationToken cancellation)
        {
            CoffeePrefill = prefill;
            return Task.CompletedTask;
        }

        public Task OpenProfileAsync(byte[] image)
        {
            ProfileImage = image;
            Events.Add("profile");
            return Task.CompletedTask;
        }

        public Task AlertAsync(
            string title,
            string message,
            CancellationToken cancellation)
        {
            Alerts.Add((title, message));
            Events.Add("alert");
            Alerted?.Invoke();
            return AlertCompletion;
        }
    }

    private sealed class TestVision : IVisionService
    {
        private int _classificationIndex;

        public PhotoWorkflowAnalysis Classification { get; set; } =
            new() { Success = true, Intent = PhotoWorkflowIntent.Unknown };
        public PhotoWorkflowAnalysis[]? Classifications { get; set; }
        public Func<PhotoWorkflowAnalysis>? ClassificationCallback { get; set; }
        public Exception? ClassificationError { get; set; }
        public BeanLabelExtraction Extraction { get; set; } =
            new() { Success = true };
        public VisionAnalysisResult RoomResult { get; set; } =
            VisionAnalysisResult.Ok(0, "No people found.");
        public int ExtractionCalls { get; private set; }
        public string? RoomQuestion { get; private set; }

        public Task<PhotoWorkflowAnalysis> ClassifyPhotoAsync(
            Stream imageStream,
            CancellationToken cancellationToken = default)
        {
            if (ClassificationError is not null)
                throw ClassificationError;
            if (ClassificationCallback is not null)
                return Task.FromResult(ClassificationCallback());
            if (Classifications is not null)
                return Task.FromResult(Classifications[_classificationIndex++]);
            return Task.FromResult(Classification);
        }

        public Task<BeanLabelExtraction> ExtractBeanLabelAsync(
            Stream imageStream,
            CancellationToken ct = default)
        {
            ExtractionCalls++;
            return Task.FromResult(Extraction);
        }

        public Task<VisionAnalysisResult> AnalyzeImageAsync(
            Stream imageStream,
            string userQuestion,
            CancellationToken cancellationToken = default)
        {
            RoomQuestion = userQuestion;
            return Task.FromResult(RoomResult);
        }

        public Task<PersonIdentificationResult> IdentifyPersonFromPhotoAsync(
            byte[] targetPhoto,
            IReadOnlyList<PersonIdentificationCandidate> candidates,
            CancellationToken ct = default) =>
            Task.FromResult(PersonIdentificationResult.NoMatch());

        public Task<bool> IsAvailableAsync() => Task.FromResult(true);
    }
}

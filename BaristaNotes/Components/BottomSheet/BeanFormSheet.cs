using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Styles;
using MauiReactor;

namespace BaristaNotes.Components;

class BeanFormState
{
    public string Name { get; set; } = string.Empty;
    public string Roaster { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public bool TrackRoastDate { get; set; }
    public DateTime RoastDate { get; set; } = DateTime.Now;
    public string Notes { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

partial class BeanFormSheet : Component<BeanFormState>
{
    private BeanDto? _bean;
    private Action<BeanDto>? _onSave;
    private Action? _onCancel;

    public BeanFormSheet Bean(BeanDto? bean)
    {
        _bean = bean;
        return this;
    }

    public BeanFormSheet OnSave(Action<BeanDto> onSave)
    {
        _onSave = onSave;
        return this;
    }

    public BeanFormSheet OnCancel(Action onCancel)
    {
        _onCancel = onCancel;
        return this;
    }

    protected override void OnMounted()
    {
        base.OnMounted();

        if (_bean != null)
        {
            SetState(s =>
            {
                s.Name = _bean.Name;
                s.Roaster = _bean.Roaster ?? string.Empty;
                s.Origin = _bean.Origin ?? string.Empty;
                s.TrackRoastDate = _bean.RoastDate.HasValue;
                s.RoastDate = _bean.RoastDate ?? DateTime.Now;
                s.Notes = _bean.Notes ?? string.Empty;
            });
        }
    }

    public override VisualNode Render()
    {
        return ScrollView(
            VStack(spacing: 12,
                Label(_bean != null ? "Edit Bean" : "Add Bean")
                    .ThemeKey(ThemeKeys.SubHeadline),

                Label("Name *")
                    .ThemeKey(ThemeKeys.SecondaryText),
                Entry()
                    .Placeholder("Bean name (required)")
                    .Text(State.Name)
                    .OnTextChanged(text => SetState(s => s.Name = text)),

                Label("Roaster")
                    .ThemeKey(ThemeKeys.SecondaryText),
                Entry()
                    .Placeholder("Roaster name")
                    .Text(State.Roaster)
                    .OnTextChanged(text => SetState(s => s.Roaster = text)),

                Label("Origin")
                    .ThemeKey(ThemeKeys.SecondaryText),
                Entry()
                    .Placeholder("Country or region of origin")
                    .Text(State.Origin)
                    .OnTextChanged(text => SetState(s => s.Origin = text)),

                HStack(spacing: 8,
                    Label("Track Roast Date")
                        .ThemeKey(ThemeKeys.SecondaryText)
                        .VCenter(),
                    Switch()
                        .IsToggled(State.TrackRoastDate)
                        .OnToggled(args => SetState(s => s.TrackRoastDate = args.Value))
                ),

                State.TrackRoastDate
                    ? DatePicker()
                        .MaximumDate(DateTime.Now)
                        .Date(State.RoastDate)
                        .OnDateSelected(date => SetState(s => s.RoastDate = date ?? DateTime.Now))
                    : null,

                Label("Notes")
                    .ThemeKey(ThemeKeys.SecondaryText),
                Editor()
                    .Placeholder("Tasting notes, processing method, etc.")
                    .Text(State.Notes)
                    .HeightRequest(80)
                    .OnTextChanged(text => SetState(s => s.Notes = text)),

                State.ErrorMessage != null
                    ? Label(State.ErrorMessage)
                        .FontSize(12)
                        .TextColor(AppColors.Error)
                    : null,

                HStack(spacing: 12,
                    Button("Cancel")
                        .ThemeKey(ThemeKeys.SecondaryButton)
                        .OnClicked(() => _onCancel?.Invoke()),
                    Button("Save")
                        .OnClicked(OnSaveClicked)
                )
                .HEnd()
            )

            .Padding(20)
            .Spacing(12)
        );
    }

    void OnSaveClicked()
    {
        // Validate
        if (string.IsNullOrWhiteSpace(State.Name))
        {
            SetState(s => s.ErrorMessage = "Bean name is required");
            return;
        }

        var beanDto = new BeanDto
        {
            Id = _bean?.Id ?? 0,
            Name = State.Name,
            Roaster = string.IsNullOrWhiteSpace(State.Roaster) ? null : State.Roaster,
            Origin = string.IsNullOrWhiteSpace(State.Origin) ? null : State.Origin,
            RoastDate = State.TrackRoastDate ? State.RoastDate : null,
            Notes = string.IsNullOrWhiteSpace(State.Notes) ? null : State.Notes,
            IsActive = true
        };

        _onSave?.Invoke(beanDto);
    }
}

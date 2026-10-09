using BaristaNotes.Core.Services;

namespace BaristaNotes.Tests.Unit;

public sealed class BeanListReadStateTests
{
    [Fact]
    public void ActiveFailureThenOriginFailure_NeverHidesRetryBehindPendingSelection()
    {
        Assert.Equal(BeanListReadState.Loading, BeanListReads.Display(false, true, false, null));
        Assert.Equal(BeanListReadState.Error, BeanListReads.Display(false, true, false, "Read failed"));
        Assert.Equal(BeanListReadState.Error, BeanListReads.Display(false, false, false, "Read failed"));
        Assert.Equal(BeanListReadState.Loading, BeanListReads.Display(false, false, false, null));
        Assert.Equal(BeanListReadState.Content, BeanListReads.Display(true, false, false, null));
    }

    [Fact]
    public void OriginFailureThenActiveFailure_AlsoShowsRetry()
    {
        Assert.Equal(BeanListReadState.Loading, BeanListReads.Display(false, false, false, null));
        Assert.Equal(BeanListReadState.Error, BeanListReads.Display(false, false, false, "Read failed"));
    }

    [Fact]
    public void ValidSelectedRows_RemainUsableWhenTheSeparateActiveReadFails()
    {
        Assert.Equal(BeanListReadState.Content, BeanListReads.Display(false, false, true, "Read failed"));
        Assert.Equal(BeanListReadState.Error, BeanListReads.Display(false, false, false, "Read failed"));
    }
}

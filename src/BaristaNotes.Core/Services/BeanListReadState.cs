namespace BaristaNotes.Core.Services;

public enum BeanListReadState { Loading, Error, Content }

public static class BeanListReads
{
    public static BeanListReadState Display(bool activeLoaded, bool selectionPending,
        bool hasSelection, string? activeError)
    {
        if (hasSelection) return BeanListReadState.Content;
        if (activeError is not null) return BeanListReadState.Error;
        return selectionPending || !activeLoaded ? BeanListReadState.Loading : BeanListReadState.Content;
    }
}

public class DraftState
{
    public bool IsDraftComplete { get; set; }
    public int Round { get; set; }
    public int Pick { get; set; }
    public List<string> DraftOrder { get; set; } = [];

    public DraftState() { }
}
namespace BoursePilotAI.Models;

public sealed class CodalAnnouncement
{
    public long TracingNo { get; set; }
    public string Symbol { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string Title { get; set; } = "";
    public string LetterCode { get; set; } = "";
    public string SentDateTime { get; set; } = "";
    public string PublishDateTime { get; set; } = "";
    public string Url { get; set; } = "";
    public string AttachmentUrl { get; set; } = "";
}

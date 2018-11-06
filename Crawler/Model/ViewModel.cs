namespace CrawlerBackendBusiness
{
    public class ViewModel
    {
        public int BrokenUrlCount { get; set; }
        public CrawlerState CrawlerState { get; set; }
        public string ElapsedTime { get; set; }
        public int IdleThreadCount { get; set; }
        public int RemainingUrlCount { get; set; }
        public string StatusText { get; set; }
        public int ValidUrlCount { get; set; }
        public int VerifiedUrlCount { get; set; }
    }
}
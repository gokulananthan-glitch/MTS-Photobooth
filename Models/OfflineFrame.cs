using System;

namespace PhotoBooth.Models
{
    public class OfflineFrame
    {
        public int Id { get; set; }
        public string FrameId { get; set; } = string.Empty;
        public string MachineCode { get; set; } = string.Empty;
        public string SiteCode { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}


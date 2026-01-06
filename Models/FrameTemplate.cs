using System;
using System.Collections.Generic;

namespace PhotoBooth.Models
{
    public class FrameTemplate
    {
        public string Id { get; set; } = string.Empty;
        public string MachineCode { get; set; } = string.Empty;
        public string Frame { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty; // Base64 or URL
        public string SiteCode { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class FrameTemplatesResponse
    {
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<FrameTemplate> Data { get; set; } = new();
    }
}


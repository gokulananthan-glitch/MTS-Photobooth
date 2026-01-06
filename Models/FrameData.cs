using System.Collections.Generic;
using System.Linq;

namespace PhotoBooth.Models
{
    public class FrameData
    {
        public string Grid { get; set; } = string.Empty;
        public int FrameW { get; set; }
        public int FrameH { get; set; }
        public int PlaceholderW { get; set; }
        public int PlaceholderH { get; set; }
        public List<Coordinate> Coordinates { get; set; } = new();
    }

    public class Coordinate
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    public static class FrameDataProvider
    {
        public static List<FrameData> GetFrameData()
        {
            return new List<FrameData>
            {
                new FrameData
                {
                    Grid = "grid1",
                    FrameW = 1240,
                    FrameH = 1844,
                    PlaceholderW = 1160,
                    PlaceholderH = 1350,
                    Coordinates = new List<Coordinate> { new Coordinate { X = 40, Y = 90 } }
                },
                new FrameData
                {
                    Grid = "grid2",
                    FrameW = 1240,
                    FrameH = 1844,
                    PlaceholderW = 1100,
                    PlaceholderH = 700,
                    Coordinates = new List<Coordinate>
                    {
                        new Coordinate { X = 70, Y = 70 },
                        new Coordinate { X = 70, Y = 800 }
                    }
                },
                new FrameData
                {
                    Grid = "grid4",
                    FrameW = 1240,
                    FrameH = 1844,
                    PlaceholderW = 580,
                    PlaceholderH = 610,
                    Coordinates = new List<Coordinate>
                    {
                        new Coordinate { X = 30, Y = 150 },
                        new Coordinate { X = 630, Y = 150 },
                        new Coordinate { X = 30, Y = 840 },
                        new Coordinate { X = 630, Y = 840 }
                    }
                },
                new FrameData
                {
                    Grid = "grid6",
                    FrameW = 1844,  // Horizontal: swapped width and height
                    FrameH = 1240,  // Horizontal: swapped width and height
                    PlaceholderW = 535,
                    PlaceholderH = 400,
                    Coordinates = new List<Coordinate>
                    {
                        // Top row: 3 images horizontally (centered)
                        // Frame width: 1844, Image width: 535, spacing: (1844 - 3*535) / 4 = 59.75 ≈ 60
                        new Coordinate { X = 60, Y = 147 },
                        new Coordinate { X = 655, Y = 147 },   // 60 + 535 + 60
                        new Coordinate { X = 1250, Y = 147 },   // 655 + 535 + 60
                        // Bottom row: 3 images horizontally (centered)
                        // Frame height: 1240, Image height: 400, spacing: (1240 - 2*400) / 3 = 146.67 ≈ 147
                        new Coordinate { X = 60, Y = 694 },    // 147 + 400 + 147
                        new Coordinate { X = 655, Y = 694 },
                        new Coordinate { X = 1250, Y = 694 }
                    }
                },
                new FrameData
                {
                    Grid = "grid8",
                    FrameW = 1240,
                    FrameH = 1844,
                    PlaceholderW = 530,
                    PlaceholderH = 350,
                    Coordinates = new List<Coordinate>
                    {
                        new Coordinate { X = 50, Y = 90 },
                        new Coordinate { X = 50, Y = 475 },
                        new Coordinate { X = 50, Y = 860 },
                        new Coordinate { X = 50, Y = 1245 },
                        new Coordinate { X = 660, Y = 90 },
                        new Coordinate { X = 660, Y = 475 },
                        new Coordinate { X = 660, Y = 860 },
                        new Coordinate { X = 660, Y = 1245 }
                    }
                },
                new FrameData
                {
                    Grid = "grid9",
                    FrameW = 1240,
                    FrameH = 1844,
                    PlaceholderW = 580,
                    PlaceholderH = 610,
                    Coordinates = new List<Coordinate>
                    {
                        new Coordinate { X = 30, Y = 150 },
                        new Coordinate { X = 630, Y = 150 },
                        new Coordinate { X = 30, Y = 840 },
                        new Coordinate { X = 630, Y = 840 }
                    }
                }
            };
        }

        public static int GetPhotoCountForStyle(int styleNumber)
        {
            return styleNumber switch
            {
                1 => 1,
                2 => 2,
                3 => 4,
                4 => 6,
                5 => 4, // grid8: 4 photos that get duplicated (4 left + 4 right = 8 total positions)
                _ => 1
            };
        }

        public static string GetGridForStyle(int styleNumber)
        {
            return styleNumber switch
            {
                1 => "grid1",
                2 => "grid2",
                3 => "grid4",
                4 => "grid6",
                5 => "grid8", // grid8 moved to style 5
                _ => "grid1"
            };
        }

        public static FrameData? GetFrameDataForStyle(int styleNumber)
        {
            string grid = GetGridForStyle(styleNumber);
            var allFrames = GetFrameData();
            return allFrames.FirstOrDefault(f => f.Grid == grid);
        }
    }
}


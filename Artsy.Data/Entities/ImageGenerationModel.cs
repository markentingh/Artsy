using System;

namespace Artsy.Data.Entities
{
    public class ImageGenerationModel
    {
        public int Id { get; set; }
        public string ModelKey { get; set; } = "";
        public string Name { get; set; } = "";
        public string Model { get; set; } = "";
        public decimal CPMITTokens { get; set; }
        public decimal CPMIITokens { get; set; }
        public decimal CPMOTokens { get; set; }
        public bool Active { get; set; } = true;
        public decimal TokenConversion { get; set; } = 10;
        public DateTime DateCreated { get; set; }
        public DateTime DateUpdated { get; set; }
    }
}

using System;

namespace Nomada.Shared.Models
{
    public class FraseDto
    {
        public int Id { get; set; }
        public string Texto { get; set; } = string.Empty;
        public string? Autor { get; set; }
    }
}
using System;
using System.Collections.Generic;

namespace Nomada.Shared.Models
{
    public class IngresoDto
    {
        public int Id { get; set; }
        public string Atleta { get; set; } = string.Empty;
        public string Coach { get; set; } = string.Empty;
        public string TipoCobro { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public DateTime FechaCobro { get; set; }
        public string? Descripcion { get; set; }
    }

    public class ResumenFinanzasDto
    {
        public decimal TotalAnio { get; set; }
        public decimal TotalMesActual { get; set; }
        public List<decimal> IngresosPorMes { get; set; } = new List<decimal>(new decimal[12]);
    }
}
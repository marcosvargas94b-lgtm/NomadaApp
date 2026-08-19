using System;
using System.Collections.Generic;

namespace Nomada.Shared.Models
{
  

    // DTO para listar las marcas de un atleta
    public class EvaluacionCatalogoDto { public int Id { get; set; } public string Nombre { get; set; } = ""; public string TipoMedida { get; set; } = ""; }
    public class EvaluacionAtletaDto { public int Id { get; set; } public int EvaluacionCatalogoId { get; set; } public string NombreEvaluacion { get; set; } = ""; public string TipoMedida { get; set; } = ""; public string Resultado { get; set; } = ""; public DateTime FechaRegistro { get; set; } public bool RegistradoPorCoach { get; set; } }
    public class CrearEvaluacionCatalogoRequest { public string GymCode { get; set; } = ""; public string Nombre { get; set; } = ""; public string TipoMedida { get; set; } = ""; }
    public class RegistrarMarcaAtletaRequest { public string GymCode { get; set; } = ""; public Guid AtletaId { get; set; } public int EvaluacionCatalogoId { get; set; } public string Resultado { get; set; } = ""; public DateTime FechaRegistro { get; set; } public bool EsCoach { get; set; } public Guid RegistradoPorId { get; set; } }
}
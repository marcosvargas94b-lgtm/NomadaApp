using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Nomada.API.Services
{
    public class GeminiAIService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public GeminiAIService(IConfiguration config)
        {
            _httpClient = new HttpClient();
            _apiKey = config["Gemini:ApiKey"];
        }

        public class RespuestaFatigaIA
        {
            public Dictionary<string, int> FatigaMuscular { get; set; } = new Dictionary<string, int>();
            public bool TienePesos { get; set; }
        }

        public async Task<RespuestaFatigaIA> AnalizarImpactoWod(string textoRutina)
        {
            try
            {
                // Usamos el modelo 2.5 Pro (o el más avanzado que tengas en tu cuota)
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-pro:generateContent?key={_apiKey}";

                string promptEstricto = $@"
Eres un experto en biomecánica deportiva y programación de CrossFit/Funcional. 
Analiza el siguiente entrenamiento (WOD) y determina 2 cosas:
1. El porcentaje de fatiga muscular BASE (0 a 100) asumiendo un esfuerzo máximo (Rx) e intensidad alta. Si un músculo no se usa, pon 0.
2. Si el entrenamiento requiere equipo con peso explícito (Mancuernas, Barras, Kettlebells, wallballs). Si es solo peso corporal, correr o saltar cuerda, es falso.

Rutina a analizar:
{textoRutina}

Reglas estrictas:
- Devuelve EXCLUSIVAMENTE un objeto JSON válido.
- No uses bloques de código (```json).
- No agregues explicaciones, notas ni saludos.
- Las llaves del diccionario DEBEN ser exactamente estas (respeta espacios y mayúsculas):
{{
  ""FatigaMuscular"": {{ ""Pecho"": 0, ""Espalda Alta"": 0, ""Lumbares"": 0, ""Hombros"": 0, ""Bíceps"": 0, ""Tríceps"": 0, ""Antebrazos"": 0, ""Abdomen"": 0, ""Oblicuos"": 0, ""Cuádriceps"": 0, ""Isquiotibiales"": 0, ""Glúteos"": 0, ""Pantorrillas"": 0, ""Trapecios"": 0, ""Full Body / Cardio"": 0 }},
  ""TienePesos"": true o false
}}";

                var requestBody = new
                {
                    contents = new[]
                    {
                        new { parts = new object[] { new { text = promptEstricto } } }
                    }
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseString);
                    var textResult = doc.RootElement
                        .GetProperty("candidates")[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text").GetString();

                    // Limpieza agresiva por si la IA devuelve formato Markdown
                    textResult = textResult.Replace("```json", "").Replace("```", "").Trim();

                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var resultado = JsonSerializer.Deserialize<RespuestaFatigaIA>(textResult, options);
                    return resultado ?? new RespuestaFatigaIA();
                }

                return new RespuestaFatigaIA();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error IA: " + ex.Message);
                return new RespuestaFatigaIA();
            }
        }

        public async Task<RespuestaFatigaIA> AjustarFatigaPorNotas(string fatigaBaseJson, string notasAtleta)
        {
            try
            {
                // Usamos gemini-2.5-pro por su capacidad superior de comprensión lectora y contexto
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-pro:generateContent?key={_apiKey}";

                string promptEstricto = $@"
Eres un experto analista de rendimiento deportivo y biomecánica.
Se te proporcionará el impacto muscular BASE de un entrenamiento y las NOTAS reales de un atleta después de realizarlo.
Tu trabajo es ajustar los porcentajes de fatiga muscular (0 a 100) basándote estrictamente en su experiencia.

Reglas de ajuste:
- Si el atleta indica que subió peso, hizo más repeticiones, o sintió el entreno extremadamente pesado/llegó al fallo, AUMENTA la fatiga.
- Si el atleta indica que bajó peso, se saltó ejercicios, modificó por dolor/lesión, o lo sintió muy suave, DISMINUYE la fatiga.
- Si las notas son irrelevantes al esfuerzo (ej. 'hizo mucho calor', 'me gustó la música'), mantén los valores base intactos.
- NUNCA excedas el 100% ni bajes de 0%.

Fatiga Base Original (JSON):
{fatigaBaseJson}

Notas del Atleta:
""{notasAtleta}""

Devuelve EXCLUSIVAMENTE un objeto JSON válido con los nuevos valores ajustados. 
- NO uses bloques de código (```json). 
- NO agregues explicaciones, saludos ni comentarios.
- Estructura OBLIGATORIA (respeta exactamente estas llaves y mayúsculas/acentos):
{{
  ""FatigaMuscular"": {{ ""Pecho"": 0, ""Espalda Alta"": 0, ""Lumbares"": 0, ""Hombros"": 0, ""Bíceps"": 0, ""Tríceps"": 0, ""Antebrazos"": 0, ""Abdomen"": 0, ""Oblicuos"": 0, ""Cuádriceps"": 0, ""Isquiotibiales"": 0, ""Glúteos"": 0, ""Pantorrillas"": 0, ""Trapecios"": 0, ""Full Body / Cardio"": 0 }},
  ""TienePesos"": true o false
}}";

                var requestBody = new
                {
                    contents = new[]
                    {
                        new { parts = new object[] { new { text = promptEstricto } } }
                    }
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseString);
                    var textResult = doc.RootElement
                        .GetProperty("candidates")[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text").GetString();

                    textResult = textResult.Replace("```json", "").Replace("```", "").Trim();

                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var resultado = JsonSerializer.Deserialize<RespuestaFatigaIA>(textResult, options);
                    return resultado ?? new RespuestaFatigaIA();
                }

                return new RespuestaFatigaIA();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error IA Ajuste: " + ex.Message);
                return new RespuestaFatigaIA(); // Si falla, que devuelva vacío para no tumbar la app
            }
        }
    }
}
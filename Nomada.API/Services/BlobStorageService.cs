using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Nomada.API.Services
{
    public interface IBlobStorageService
    {
        // Declaramos que este servicio hace 2 cosas: Subir videos y subir fotos
        Task<string> SubirVideoCatalogoAsync(IFormFile archivo);
        Task<string> SubirImagenUsuarioAsync(IFormFile archivo);
    }

    public class BlobStorageService : IBlobStorageService
    {
        private readonly string _connectionString;

        public BlobStorageService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("AzureBlobStorage");
        }

        // ================= VIDEOS =================
        public async Task<string> SubirVideoCatalogoAsync(IFormFile archivo)
        {
            if (archivo == null || archivo.Length == 0) return null;

            var blobServiceClient = new BlobServiceClient(_connectionString);
            var blobContainerClient = blobServiceClient.GetBlobContainerClient("catalogo-videos"); // Contenedor de WODs

            string extension = Path.GetExtension(archivo.FileName);
            string nombreArchivo = $"{Guid.NewGuid()}{extension}";

            var blobClient = blobContainerClient.GetBlobClient(nombreArchivo);

            using var stream = archivo.OpenReadStream();
            await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = archivo.ContentType });

            return blobClient.Uri.ToString();
        }

        // ================= IMÁGENES DE PERFIL =================
        public async Task<string> SubirImagenUsuarioAsync(IFormFile archivo)
        {
            if (archivo == null || archivo.Length == 0) return null;

            var blobServiceClient = new BlobServiceClient(_connectionString);
            var blobContainerClient = blobServiceClient.GetBlobContainerClient("fotos-usuarios"); // Contenedor de Perfiles

            string extension = Path.GetExtension(archivo.FileName);
            string nombreArchivo = $"{Guid.NewGuid()}{extension}";

            var blobClient = blobContainerClient.GetBlobClient(nombreArchivo);

            using var stream = archivo.OpenReadStream();
            await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = archivo.ContentType });

            return blobClient.Uri.ToString();
        }
    }
}
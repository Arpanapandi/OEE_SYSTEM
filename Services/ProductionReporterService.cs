using System.Net.Http.Json;

namespace OeeSystem.Services
{
    // Model data kecil (boleh ditaruh disini biar praktis)
    public class ProductionPayload 
    {
        public string? apiKey { get; set; } // huruf kecil biar cocok dgn JSON
        public int count { get; set; }
        public string? status { get; set; }
    }

    public class ProductionReporterService
    {
        private readonly HttpClient _http;

        // Inject HttpClient otomatis
        public ProductionReporterService(HttpClient http)
        {
            _http = http;
        }

        public async Task LaporServer(int jumlah = 1)
        {
            // Ganti URL ini dengan IP Laptop Server Monitoring
            // Default: http://localhost:5292/api/production. Sesuaikan jika perlu.
            string url = "http://localhost:5292/api/production"; 
            
            var data = new ProductionPayload
            {
                apiKey = "MASUKKAN_KEY_DARI_DASHBOARD",
                count = jumlah,
                status = "Online"
            };
            
            try 
            {
                await _http.PostAsJsonAsync(url, data);
            }
            catch (Exception ex)
            {
                // Silent fail or log error so it doesn't break the main app flow
                Console.WriteLine($"Gagal lapor server: {ex.Message}");
            }
        }
    }
}

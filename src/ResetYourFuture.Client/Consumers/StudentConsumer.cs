using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ResetYourFuture.Shared.Models;
using ResetYourFuture.Client.Interfaces;

namespace ResetYourFuture.Client.Consumers;
public class StudentConsumer : IStudentService
{
    private readonly HttpClient _httpClient;

    public StudentConsumer(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<IEnumerable<Student>> GetAllAsync()
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<Student>>("api/students")
               ?? new List<Student>();
    }
}
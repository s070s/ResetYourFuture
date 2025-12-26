using System.Collections.Generic;
using System.Threading.Tasks;
using ResetYourFuture.Shared.Models;

namespace ResetYourFuture.Client.Interfaces;
public interface IStudentService
{
    Task<IEnumerable<Student>> GetAllAsync();
}
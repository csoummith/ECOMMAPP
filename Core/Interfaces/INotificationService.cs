using ECOMMAPP.Core.Entities;
using System.Threading.Tasks;

namespace ECOMMAPP.Core.Interfaces
{
    public interface INotificationService
    {
        Task SendOrderFulfillmentNotificationAsync(Order order);
    }
}
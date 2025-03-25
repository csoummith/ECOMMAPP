using System;

namespace ECOMMAPP.Core.Exceptions
{
    public class OrderNotFoundException : Exception
    {
        public int OrderId { get; }

        public OrderNotFoundException(int orderId)
            : base($"Order with ID {orderId} was not found.")
        {
            OrderId = orderId;
        }
    }
}
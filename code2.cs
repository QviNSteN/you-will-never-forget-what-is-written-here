namespace WebApplication
{
    public class Product
    {
        //Модель из 20 полей
        //...
        public string Comment { get; set; }
        public int CommentStatus { get; set; }
    }
     
    [ApiController]
    public class OrdersController : ControllerBase
    {
        //... конструктор пропущен
         
        private async Task<IEnumerable<Product>> GetProductsAsync(long orderId)
        {
            using (var conn = db.CreateConnection())
            {
                var sql = "select * from Products where orderId = @orderId";
                return await conn.QueryAsync<Product>(sql, new {orderId}).ToList();
            }
        }
         
        private OrderModel GetOrderInfoFromExternalSite(long externalId)
        {
            return new HttpClient()
                .GetObjectAsync<OrderModel>($"http://external.site.com/get-order-info?id={externalId}")
                .GetAwaiter().GetResult();
        }
        
        [HttpPost("/update-order-data")]
        public async Task UpdateOrderDataAsync([FromBody] OrderModel orderModel)
        {
            var orderId = this.GetOrderIdFromSession();
            var order = this.GetOrderInfoFromExternalSite(orderId);
            var products = await this.GetProductsAsync(orderId);
             
            order.Name = orderModel.Name;
 
            foreach (var product in products)
            {
                if (order.Comments[product.Comment] != null)
                {
                    product.CommentStatus = order.Comments[product.Comment].Status;
                     
                    await this.productsRepository.UpdateAsync(products);
                }
            }
            
 
            await this.orderRepository.UpdateAsync(order);
            
            await this.rabbitClient.PublishAsync(new UpdateOrderEvent(orderId)).ConfigureAwait(false);
        }
    }
}

namespace Test.RestApi
{
    /// <summary>
    /// Request body for creating a product.
    /// </summary>
    public class CreateProductRequest
    {
        /// <summary>
        /// Product name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Product price.
        /// </summary>
        public decimal Price { get; set; }
    }
}

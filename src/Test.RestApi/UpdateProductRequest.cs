namespace Test.RestApi
{
    /// <summary>
    /// Request body for updating a product.
    /// </summary>
    public class UpdateProductRequest
    {
        /// <summary>
        /// Updated product name, or null to leave unchanged.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Updated product price, or null to leave unchanged.
        /// </summary>
        public decimal? Price { get; set; }
    }
}

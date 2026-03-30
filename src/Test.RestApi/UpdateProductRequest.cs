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
        public string Name { get; set; } = null;

        /// <summary>
        /// Updated product price, or null to leave unchanged.
        /// </summary>
        public decimal? Price
        {
            get
            {
                return _Price;
            }
            set
            {
                if (value.HasValue && value.Value < 0) _Price = 0;
                else _Price = value;
            }
        }

        private decimal? _Price = null;
    }
}

namespace Test.RestApi
{
    using System;

    /// <summary>
    /// Product model.
    /// </summary>
    public class Product
    {
        /// <summary>
        /// Product identifier.
        /// </summary>
        public Guid Id { get; set; }

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

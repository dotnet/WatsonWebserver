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
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                _Name = value ?? String.Empty;
            }
        }

        /// <summary>
        /// Product price.
        /// </summary>
        public decimal Price
        {
            get
            {
                return _Price;
            }
            set
            {
                if (value < 0) _Price = 0;
                else _Price = value;
            }
        }

        private string _Name = String.Empty;
        private decimal _Price = 0;
    }
}

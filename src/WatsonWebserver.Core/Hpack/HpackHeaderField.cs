namespace WatsonWebserver.Core.Hpack
{
    using System;

    /// <summary>
    /// HPACK header field.
    /// </summary>
    public class HpackHeaderField
    {
        /// <summary>
        /// Header name.
        /// </summary>
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Name));
                _Name = value;
            }
        }

        /// <summary>
        /// Header value.
        /// </summary>
        public string Value
        {
            get
            {
                return _Value;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Value));
                _Value = value;
            }
        }

        private string _Name = String.Empty;
        private string _Value = String.Empty;
    }
}

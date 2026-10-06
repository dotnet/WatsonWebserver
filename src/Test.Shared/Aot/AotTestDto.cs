namespace Test.Shared.Aot
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Net;

    /// <summary>
    /// Application type covering the value kinds Watson's serializer handles specially.
    /// </summary>
    public class AotTestDto
    {
        /// <summary>
        /// Name.
        /// </summary>
        public string Name { get; set; } = "name";

        /// <summary>
        /// Always null, to verify null properties are omitted.
        /// </summary>
        public string Nothing { get; set; } = null;

        /// <summary>
        /// Count.
        /// </summary>
        public int Count { get; set; } = 3;

        /// <summary>
        /// Ratio.
        /// </summary>
        public double Ratio { get; set; } = 0.25;

        /// <summary>
        /// Price.
        /// </summary>
        public decimal Price { get; set; } = 9.99m;

        /// <summary>
        /// Flag.
        /// </summary>
        public bool Flag { get; set; } = true;

        /// <summary>
        /// Shade.
        /// </summary>
        public AotTestShade Shade { get; set; } = AotTestShade.Dark;

        /// <summary>
        /// Timestamp, written by Watson's DateTime converter.
        /// </summary>
        public DateTime When { get; set; } = new DateTime(2026, 1, 2, 3, 4, 5, 678, DateTimeKind.Utc);

        /// <summary>
        /// Identifier.
        /// </summary>
        public Guid Id { get; set; } = Guid.Parse("11111111-2222-3333-4444-555555555555");

        /// <summary>
        /// Address, written by Watson's IPAddress converter.
        /// </summary>
        public IPAddress Address { get; set; } = IPAddress.Parse("10.1.2.3");

        /// <summary>
        /// Headers, written by Watson's NameValueCollection converter.
        /// </summary>
        public NameValueCollection Headers { get; set; } = new NameValueCollection { { "a", "1" }, { "a", "2" }, { "b", "x<y>&'" } };

        /// <summary>
        /// Items.
        /// </summary>
        public List<AotTestItem> Items { get; set; } = new List<AotTestItem> { new AotTestItem { Number = 1, Shade = AotTestShade.Light } };

        /// <summary>
        /// Object-typed bag whose values are resolved by runtime type.
        /// </summary>
        public Dictionary<string, object> Bag { get; set; } = new Dictionary<string, object> { { "s", "v" }, { "i", 7 }, { "nested", new AotTestItem { Number = 2 } }, { "nul", null } };
    }
}

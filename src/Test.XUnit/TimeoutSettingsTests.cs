namespace Test.XUnit
{
    using System;
    using WatsonWebserver.Core;
    using Xunit;

    public class TimeoutSettingsTests
    {
        [Fact]
        public void Default_IsZero()
        {
            TimeoutSettings settings = new TimeoutSettings();
            Assert.Equal(TimeSpan.Zero, settings.DefaultTimeout);
        }

        [Fact]
        public void Constructor_SetsTimeout()
        {
            TimeoutSettings settings = new TimeoutSettings(TimeSpan.FromSeconds(30));
            Assert.Equal(TimeSpan.FromSeconds(30), settings.DefaultTimeout);
        }

        [Fact]
        public void NegativeTimeout_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                TimeoutSettings settings = new TimeoutSettings();
                settings.DefaultTimeout = TimeSpan.FromSeconds(-1);
            });
        }
    }
}

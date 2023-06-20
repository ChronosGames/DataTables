using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace DataTables.Tests
{
    public class DataTableProcessTest
    {
        private static string ConvertToDigit(int num)
        {
            if (num < 26)
            {
                return Convert.ToString((char)('A' + num));
            }
            else
            {
                return ConvertToDigit(num / 26 - 1) + ConvertToDigit(num % 26);
            }
        }

        [Fact]
        public void ConvertNumberToLetterTest()
        {
            ConvertToDigit(0).Should().Be("A");
            ConvertToDigit(25).Should().Be("Z");
            ConvertToDigit(26).Should().Be("AA");
            ConvertToDigit(27).Should().Be("AB");
            ConvertToDigit(27 * 26).Should().Be("AAA");
        }
    }
}

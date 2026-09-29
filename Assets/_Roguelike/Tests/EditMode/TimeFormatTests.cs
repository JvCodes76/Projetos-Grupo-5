using NUnit.Framework;
using Roguelike.Run;

namespace Roguelike.Tests
{
    /// <summary>Testes de <see cref="TimeFormat"/> (tarefa 3.2).</summary>
    public class TimeFormatTests
    {
        [Test]
        public void Format_TruncatesTenthDown()
        {
            Assert.AreEqual("00:18.4", TimeFormat.Format(18.4f));
        }

        [Test]
        public void Format_PadsMinutesAndSeconds()
        {
            Assert.AreEqual("01:05.0", TimeFormat.Format(65.05f));
        }

        [Test]
        public void Format_Zero_ReturnsZeroTime()
        {
            Assert.AreEqual("00:00.0", TimeFormat.Format(0f));
        }

        [Test]
        public void Format_Negative_TreatedAsZero()
        {
            Assert.AreEqual("00:00.0", TimeFormat.Format(-5f));
        }

        [Test]
        public void Format_NaN_ReturnsPlaceholder()
        {
            Assert.AreEqual("--:--.-", TimeFormat.Format(float.NaN));
        }

        [Test]
        public void Format_OneHourOrMore_UsesHoursFormat()
        {
            Assert.AreEqual("1:01:01.4", TimeFormat.Format(3661.4f));
        }

        [Test]
        public void Format_ExactlyOneHour()
        {
            Assert.AreEqual("1:00:00.0", TimeFormat.Format(3600f));
        }

        [Test]
        public void Format_Tenth_RobustToFloatRepresentationError()
        {
            // 0.3f é armazenado como ~0.30000001192: sem epsilon no truncamento o décimo sairia ".3" mesmo
            // assim (3.0000001 trunca para 3), mas o caso citado no plano garante que não regride para ".2".
            Assert.AreEqual("00:00.3", TimeFormat.Format(0.3f));
        }

        [Test]
        public void Format_TruncatesWithoutRoundingUp()
        {
            // 18.49 deve truncar para 18.4, nunca arredondar para 18.5.
            Assert.AreEqual("00:18.4", TimeFormat.Format(18.49f));
        }
    }
}

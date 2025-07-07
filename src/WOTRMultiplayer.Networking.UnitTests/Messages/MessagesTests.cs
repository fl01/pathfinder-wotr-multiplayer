using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WOTRMultiplayer.Networking.Messages;

namespace WOTRMultiplayer.Networking.UnitTests.Messages
{
    [TestFixture]
    public class MessagesTests
    {
        [Test]
        public void NetworkMessages_HaveUniqueIds()
        {
            // Arrange
            var allMessages = Assembly
                .GetAssembly(typeof(ProtobufPacket))
                .GetTypes()
                .Where(t => t.GetCustomAttribute<BeetleX.Packets.MessageTypeAttribute>() != null)
                .Select(t => new { Type = t, MessageType = t.GetCustomAttribute<BeetleX.Packets.MessageTypeAttribute>() })
                .ToList();

            // Act
            var duplicateIds = allMessages.GroupBy(x => (int)x.MessageType.ID).Where(x => x.Count() > 1).ToList();

            // Assert
            Assert.That(duplicateIds.Count, Is.EqualTo(0), "Duplicate Ids: " + string.Join(", ", duplicateIds.Select(x => $"{x.Key} ({string.Join(",", x.Select(a => a.Type.Name).ToList())})")));
        }
    }
}

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

        [TestCase(100, 1000, "Lobby")]
        [TestCase(1000, 2000, "Game")]
        public void NetworkMessages_NoMissingIds(int start, int end, string groupName)
        {
            // Arrange
            var allMessages = Assembly
                .GetAssembly(typeof(ProtobufPacket))
                .GetTypes()
                .Where(t => t.GetCustomAttribute<BeetleX.Packets.MessageTypeAttribute>() != null)
                .Select(t => new { Type = t, MessageType = t.GetCustomAttribute<BeetleX.Packets.MessageTypeAttribute>() })
                .ToList();
            var messagesInRange = allMessages
                .Where(x => (int)x.MessageType.ID >= start && (int)x.MessageType.ID < end)
                .OrderBy(x => (int)x.MessageType.ID)
                .ToList();
            var expectedRange = Enumerable.Range(start, messagesInRange.Count).ToList();

            // Act
            var missingIds = expectedRange.Except(messagesInRange.Select(x => (int)x.MessageType.ID));

            // Assert
            Assert.That(missingIds.Count, Is.EqualTo(0), $"{groupName} missing Ids: " + string.Join(",", missingIds.Select(x => x.ToString())));
        }
    }
}

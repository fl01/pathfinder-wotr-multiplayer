using System;
using System.Collections.Generic;
using System.Linq;
using FakeItEasy;
using Kingmaker.EntitySystem;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.Hashing;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.Services.Random;

namespace WOTRMultiplayer.UnitTests.Services.Random
{
    [TestFixture]
    public class PredictableValueGeneratorTests
    {
        private ILogger<PredictableValueGenerator> _logger;
        private PredictableValueGenerator _predictableValueGenerator;
        private IHashService _hashService;
        private IGameInteractionService _gameInteractionService;

        [SetUp]
        public void SetUp()
        {
            _hashService = A.Fake<IHashService>();
            _gameInteractionService = A.Fake<IGameInteractionService>();
            _logger = A.Fake<ILogger<PredictableValueGenerator>>();

            _predictableValueGenerator = new PredictableValueGenerator(_logger, _gameInteractionService, _hashService);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(5)]
        public void GenerateUniqueId_SameIdentifierWithExistingEntities_IdCounterIsCorrect(int numberOfExistingEntities)
        {
            // Arrange
            var gameId = Guid.NewGuid().ToString();
            var idType = UniqueIdType.Unit;
            var expectedPrefix = idType.GetAttributeOfType<System.ComponentModel.DescriptionAttribute>().Description;
            var identifier = "bla";
            var hash = 123;
            var expectedCounter = numberOfExistingEntities + 1;
            A.CallTo(() => _hashService.Murmur3(idType + identifier)).Returns(hash);
            A.CallTo(() => _gameInteractionService.GetEntity(A<string>.Ignored)).Returns(null);
            for (int i = 0; i < numberOfExistingEntities; i++)
            {
                A.CallTo(() => _gameInteractionService.GetEntity(A<string>.That.EndsWith((i + 1).ToString()))).Returns(A.Fake<EntityDataBase>());
            }

            // Act
            var actualId = _predictableValueGenerator.GenerateUniqueId(idType, gameId, identifier);
            // Assert
            Assert.That(actualId, Does.StartWith(expectedPrefix));
            Assert.That(actualId, Does.EndWith(expectedCounter.ToString()));
            Assert.That(actualId, Does.Contain(hash.ToString()));
            A.CallTo(() => _gameInteractionService.GetEntity(A<string>.That.Contains(hash.ToString()))).MustHaveHappened(expectedCounter, Times.Exactly);
        }

        [Test]
        public void GenerateUniqueId_HashServiceThrowsException_ExceptionIsRethrown()
        {
            // Arrange
            var gameId = Guid.NewGuid().ToString();
            var idType = UniqueIdType.Unit;
            var expectedPrefix = idType.GetAttributeOfType<System.ComponentModel.DescriptionAttribute>().Description;
            var identifier = "bla";
            var expectedException = new InvalidOperationException(Guid.NewGuid().ToString());
            A.CallTo(() => _hashService.Murmur3(A<string>.Ignored)).Throws(expectedException);

            // Act
            var actualException = Assert.Throws<InvalidOperationException>(() => _predictableValueGenerator.GenerateUniqueId(idType, gameId, identifier));

            // Assert
            Assert.That(actualException.Message, Is.EqualTo(expectedException.Message));
            A.CallTo(() => _gameInteractionService.GetEntity(A<string>.Ignored)).MustNotHaveHappened();
        }

        [Test]
        public void GenerateUniqueId_GameInteractionServiceThrowsException_ExceptionIsRethrown()
        {
            // Arrange
            var gameId = Guid.NewGuid().ToString();
            var idType = UniqueIdType.Unit;
            var expectedPrefix = idType.GetAttributeOfType<System.ComponentModel.DescriptionAttribute>().Description;
            var identifier = "bla";
            var expectedException = new InvalidOperationException(Guid.NewGuid().ToString());
            A.CallTo(() => _gameInteractionService.GetEntity(A<string>.Ignored)).Throws(expectedException);

            // Act
            var actualException = Assert.Throws<InvalidOperationException>(() => _predictableValueGenerator.GenerateUniqueId(idType, gameId, identifier));

            // Assert
            Assert.That(actualException.Message, Is.EqualTo(expectedException.Message));
        }

        [TestCaseSource(nameof(GetAllUniqueIdTypes))]
        public void GenerateUniqueId_MultipleIdentifiers_IdCounterIsUniqueToIdentifier(UniqueIdType idType)
        {
            // Arrange
            var gameId = Guid.NewGuid().ToString();
            var identifierOne = "bla";
            var identifierTwo = "bla-bla";
            var hashOne = 123;
            var hashTwo = 456;
            A.CallTo(() => _hashService.Murmur3(idType + identifierOne)).Returns(hashOne);
            A.CallTo(() => _hashService.Murmur3(idType + identifierTwo)).Returns(hashTwo);
            A.CallTo(() => _gameInteractionService.GetEntity(A<string>.Ignored)).Returns(null);

            // Act
            var actualIdOne = _predictableValueGenerator.GenerateUniqueId(idType, gameId, identifierOne);
            var actualIdTwo = _predictableValueGenerator.GenerateUniqueId(idType, gameId, identifierTwo);

            // Assert
            Assert.That(actualIdOne, Does.EndWith("1"));
            Assert.That(actualIdTwo, Does.EndWith("1"));
            Assert.That(actualIdOne, Is.Not.EqualTo(actualIdTwo));
            A.CallTo(() => _gameInteractionService.GetEntity(A<string>.Ignored)).MustHaveHappened(2, Times.Exactly);
        }

        [TestCaseSource(nameof(GetAllUniqueIdTypes))]
        public void GenerateUniqueId_MultipleCallsForSameIdentifierWithSameIdType_IdCounterIsShared(UniqueIdType idType)
        {
            // Arrange
            var gameId = Guid.NewGuid().ToString();
            var identifierOne = "bla";
            var hashOne = 123;
            A.CallTo(() => _hashService.Murmur3(idType + identifierOne)).Returns(hashOne);
            A.CallTo(() => _gameInteractionService.GetEntity(A<string>.Ignored)).Returns(null);

            // Act
            var actualIdOne = _predictableValueGenerator.GenerateUniqueId(idType, gameId, identifierOne);
            var actualIdTwo = _predictableValueGenerator.GenerateUniqueId(idType, gameId, identifierOne);

            // Assert
            Assert.That(actualIdOne, Does.EndWith("1"));
            Assert.That(actualIdTwo, Does.EndWith("2"));
            Assert.That(actualIdOne, Is.Not.EqualTo(actualIdTwo));
            A.CallTo(() => _gameInteractionService.GetEntity(A<string>.Ignored)).MustHaveHappened(2, Times.Exactly);
        }

        [TestCaseSource(nameof(GetAllUniqueIdTypes))]
        public void GenerateUniqueId_MultipleCallsForSameIdentifierWithSameIdTypeButDifferentGameId_IdCounterIsNotShared(UniqueIdType idType)
        {
            // Arrange
            var gameId = Guid.NewGuid().ToString();
            var identifierOne = "bla";
            var hashOne = 123;
            A.CallTo(() => _hashService.Murmur3(idType + identifierOne)).Returns(hashOne);
            A.CallTo(() => _gameInteractionService.GetEntity(A<string>.Ignored)).Returns(null);

            // Act
            var actualIdOne = _predictableValueGenerator.GenerateUniqueId(idType, gameId, identifierOne);
            var actualIdTwo = _predictableValueGenerator.GenerateUniqueId(idType, Guid.NewGuid().ToString(), identifierOne);

            // Assert
            Assert.That(actualIdOne, Does.EndWith("1"));
            Assert.That(actualIdTwo, Does.EndWith("1"));
            Assert.That(actualIdOne, Is.EqualTo(actualIdTwo));
            A.CallTo(() => _gameInteractionService.GetEntity(A<string>.Ignored)).MustHaveHappened(2, Times.Exactly);
        }

        [TestCase(UniqueIdType.Unit, UniqueIdType.EntityView)]
        [TestCase(UniqueIdType.AreaEffect, UniqueIdType.ItemEntity)]
        [TestCase(UniqueIdType.CustomCompanionUnit, UniqueIdType.Unit)]
        public void GenerateUniqueId_MultipleCallsForSameIdentifierButDifferentIdType_IdCounterIsNotShared(UniqueIdType idTypeOne, UniqueIdType idTypeTwo)
        {
            // Arrange
            var gameId = Guid.NewGuid().ToString();
            var identifierOne = "bla";
            var hashOne = 123;
            var hashTwo = 456;
            A.CallTo(() => _hashService.Murmur3(idTypeOne + identifierOne)).Returns(hashOne);
            A.CallTo(() => _hashService.Murmur3(idTypeTwo + identifierOne)).Returns(hashTwo);
            A.CallTo(() => _gameInteractionService.GetEntity(A<string>.Ignored)).Returns(null);

            // Act
            var actualIdOne = _predictableValueGenerator.GenerateUniqueId(idTypeOne, gameId, identifierOne);
            var actualIdTwo = _predictableValueGenerator.GenerateUniqueId(idTypeTwo, gameId, identifierOne);

            // Assert
            Assert.That(actualIdOne, Does.EndWith("1"));
            Assert.That(actualIdTwo, Does.EndWith("1"));
            Assert.That(actualIdOne, Is.Not.EqualTo(actualIdTwo));
            A.CallTo(() => _gameInteractionService.GetEntity(A<string>.Ignored)).MustHaveHappened(2, Times.Exactly);
        }

        private static IEnumerable<UniqueIdType> GetAllUniqueIdTypes()
        {
            return [.. Enum.GetValues(typeof(UniqueIdType)).Cast<UniqueIdType>()];
        }
    }
}

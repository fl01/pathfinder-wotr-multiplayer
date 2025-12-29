using System.Collections.Generic;
using System.Linq;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.Localization;
using WOTRMultiplayer.Services.Localization;

namespace WOTRMultiplayer.UnitTests.Services.Localization
{
    [TestFixture]
    public class LocalizationServiceTests
    {
        private LocalizationService _localizationService;
        private IFileSystemService _fileSystemService;
        private ILocalizationManagerAccessor _localizationManagerAccessor;
        private ILogger<LocalizationService> _logger;

        [SetUp]
        public void SetUp()
        {
            _fileSystemService = A.Fake<IFileSystemService>();
            _localizationManagerAccessor = A.Fake<ILocalizationManagerAccessor>();
            _logger = A.Fake<ILogger<LocalizationService>>();

            _localizationService = new LocalizationService(_logger, _fileSystemService, _localizationManagerAccessor);
        }

        [TestCase(LocalizationService.FallbackLocale)]
        [TestCase("any other locale")]
        public void UpdateLocale_DefaultLocaleExists_AllKeysAreConfigured(string locale)
        {
            // Arrange
            var baseJson = @"{
                            ""wotrmultiplayer"": {
                                ""settings"": {
                                    ""general"": {
                                        ""playerName"": {
                                            ""description"": ""bla bla bla"",
                                            ""tooltip"": ""tooltip tooltip""
                                        }
                                    }
                                }
                            }
                        }";
            A.CallTo(() => _fileSystemService.GetFileContent(A<string>.That.Contains(LocalizationService.FallbackLocale))).Returns(baseJson);
            var expectedKeys = new List<KeyValuePair<string, string>>()
            {
               new("wotrmultiplayer.settings.general.playerName.description", "bla bla bla"),
               new("wotrmultiplayer.settings.general.playerName.tooltip", "tooltip tooltip"),
            };

            // Act
            _localizationService.UpdateLocale(locale);

            // Assert
            var calls = Fake.GetCalls(_localizationManagerAccessor);
            Assert.That(calls.Count(), Is.EqualTo(1), "Localization manager accessor has not been called expected number of times");
            var translations = calls.First().Arguments.First() as Dictionary<string, string>;
            Assert.That(translations.Count, Is.EqualTo(expectedKeys.Count));
            foreach (var kv in expectedKeys)
            {
                Assert.That(translations, Contains.Key(kv.Key));
                Assert.That(translations[kv.Key], Is.EqualTo(kv.Value));
            }
        }

        [Test]
        public void UpdateLocale_Locale_KeysAreMergedWithBaseLocaleAndConfigured()
        {
            // Arrange
            var baseJson = @"{
                            ""wotrmultiplayer"": {
                                ""settings"": {
                                    ""general"": {
                                        ""playerName"": {
                                            ""description"": ""bla bla bla"",
                                            ""tooltip"": ""tooltip tooltip""
                                        }
                                    }
                                }
                            }
                        }";
            A.CallTo(() => _fileSystemService.GetFileContent(A<string>.That.Contains(LocalizationService.FallbackLocale))).Returns(baseJson);

            var locale = "i am locale hehe";
            var localeJson = @"{
                            ""wotrmultiplayer"": {
                                ""settings"": {
                                    ""general"": {
                                        ""playerName"": {
                                            ""description"": ""should be overriden"",
                                        }
                                    }
                                }
                            }
                        }";
            A.CallTo(() => _fileSystemService.GetFileContent(A<string>.That.Contains(locale))).Returns(localeJson);

            var expectedKeys = new List<KeyValuePair<string, string>>()
            {
               new("wotrmultiplayer.settings.general.playerName.description", "should be overriden"),
               new("wotrmultiplayer.settings.general.playerName.tooltip", "tooltip tooltip"),
            };

            // Act
            _localizationService.UpdateLocale(locale);

            // Assert
            var calls = Fake.GetCalls(_localizationManagerAccessor);
            Assert.That(calls.Count(), Is.EqualTo(1), "Localization manager accessor has not been called expected number of times");
            var translations = calls.First().Arguments.First() as Dictionary<string, string>;
            Assert.That(translations.Count, Is.EqualTo(expectedKeys.Count));
            foreach (var kv in expectedKeys)
            {
                Assert.That(translations, Contains.Key(kv.Key));
                Assert.That(translations[kv.Key], Is.EqualTo(kv.Value));
            }
        }

        [TestCase(LocalizationService.FallbackLocale)]
        [TestCase("any other locale")]
        public void UpdateLocale_MissingBaseLocale_LocalizationManagerIsNotUpdated(string locale)
        {
            // Act
            _localizationService.UpdateLocale(locale);

            // Assert
            A.CallTo(() => _localizationManagerAccessor.UpdateCurrentLocalePack(A<Dictionary<string, string>>.Ignored)).MustNotHaveHappened();
        }
    }
}

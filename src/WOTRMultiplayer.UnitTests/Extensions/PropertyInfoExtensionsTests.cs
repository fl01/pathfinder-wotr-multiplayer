using NUnit.Framework;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.UnitTests.Extensions
{
    [TestFixture]
    public class PropertyInfoExtensionsTests
    {
        public const int DefaultReferencePropertyValue = 11;
        public const int DefaultValuePropertyValue = 33;

        public class TestSubject
        {
            public TestReferenceProperty ReferenceNoSetterProperty { get; }

            public int ValueNoSetterProperty { get; }

            public TestReferenceProperty ReferenceSetterProperty { get; private set; }

            public int ValueSetterProperty { get; private set; }

            public TestSubject()
            {
                ReferenceNoSetterProperty = new TestReferenceProperty { Value = DefaultReferencePropertyValue };
                ValueNoSetterProperty = DefaultValuePropertyValue;
                ReferenceSetterProperty = new TestReferenceProperty { Value = DefaultReferencePropertyValue };
                ValueSetterProperty = DefaultValuePropertyValue;
            }
        }

        public class TestReferenceProperty
        {
            public int Value { get; set; }
        }

        [TestCase(nameof(TestSubject.ReferenceNoSetterProperty), 100)]
        [TestCase(nameof(TestSubject.ReferenceSetterProperty), 101)]
        public void SetPropertyValue_ReferenceType_SetterIsSet(string propertyName, int expectedValue)
        {
            // Arrange
            var testSubject = new TestSubject();
            var propertyInfo = testSubject.GetType().GetProperty(propertyName);
            var expected = new TestReferenceProperty { Value = expectedValue };

            // Act
            propertyInfo.SetPropertyValue(testSubject, expected);

            // Assert
            Assert.That(((TestReferenceProperty)propertyInfo.GetValue(testSubject)).Value, Is.EqualTo(expectedValue));
            Assert.That(propertyInfo.GetValue(testSubject).GetHashCode(), Is.EqualTo(expected.GetHashCode()));
        }

        [TestCase(nameof(TestSubject.ValueNoSetterProperty), 102)]
        [TestCase(nameof(TestSubject.ValueSetterProperty), 103)]
        public void SetPropertyValue_ValueType_SetterIsSet(string propertyName, int expectedValue)
        {
            // Arrange
            var testSubject = new TestSubject();
            var propertyInfo = testSubject.GetType().GetProperty(propertyName);

            // Act
            propertyInfo.SetPropertyValue(testSubject, expectedValue);

            // Assert
            Assert.That(propertyInfo.GetValue(testSubject), Is.EqualTo(expectedValue));
        }
    }
}

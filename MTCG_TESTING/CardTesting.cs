using Moq;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using SWE.Models;

namespace SWE.Models.Tests
{
    public class CardTests
    {
        private readonly Mock<IDatabaseConnection> _mockDbConnection;
        private readonly Card _card;

        public CardTests()
        {
            _mockDbConnection = new Mock<IDatabaseConnection>();
            _card = new Card(_mockDbConnection.Object);
        }

        [Fact]
        public void SetCardTypeAndElement_ShouldSetCorrectTypeAndElement()
        {
            // Arrange
            var cardName = "Fire Dragon";
            _card.name = cardName;

            // Act
            _card.SetCardTypeAndElement();

            // Assert
            Assert.Equal(CardType.Monster, _card.Type);
            Assert.Equal(ElementType.Fire, _card.Element);
        }

        [Fact]
        public void SetCardTypeAndElement_ShouldSetSpellType_WhenNameContainsSpell()
        {
            // Arrange
            var cardName = "Fire Spell";
            _card.name = cardName;

            // Act
            _card.SetCardTypeAndElement();

            // Assert
            Assert.Equal(CardType.Spell, _card.Type);
            Assert.Equal(ElementType.Fire, _card.Element);
        }

        [Fact]
        public void GetCardType_ShouldReturnMonster_WhenCardNameContainsMonster()
        {
            // Act
            var cardType = Card.GetCardType("Fire Monster");

            // Assert
            Assert.Equal(CardType.Monster, cardType);
        }

        [Fact]
        public void GetCardType_ShouldReturnSpell_WhenCardNameContainsSpell()
        {
            // Act
            var cardType = Card.GetCardType("Water Spell");

            // Assert
            Assert.Equal(CardType.Spell, cardType);
        }

        [Fact]
        public void GetElementalType_ShouldReturnFire_WhenCardNameContainsFire()
        {
            // Act
            var elementType = Card.GetElementalType("Fire Dragon");

            // Assert
            Assert.Equal(ElementType.Fire, elementType);
        }

        [Fact]
        public void GetElementalType_ShouldReturnWater_WhenCardNameContainsWater()
        {
            // Act
            var elementType = Card.GetElementalType("Water Spell");

            // Assert
            Assert.Equal(ElementType.Water, elementType);
        }

        [Fact]
        public async Task AddCardAsync_ShouldInsertCardAndPackage()
        {
            // Arrange
            var card = new Card("Fire Dragon")
            {
                id = Guid.NewGuid(),
                name = "Fire Dragon",
                damage = 50.0,
                Type = CardType.Monster,
                Element = ElementType.Fire
            };
            _mockDbConnection.Setup(c => c.OpenAsync()).Returns(Task.CompletedTask);
            _mockDbConnection.Setup(c => c.ExecuteScalarAsync(It.IsAny<string>(), It.IsAny<NpgsqlParameter[]>()))
                .ReturnsAsync(1); // Simulate the insert returning package_id as 1.
            _mockDbConnection.Setup(c => c.ExecuteNonQueryAsync(It.IsAny<string>(), It.IsAny<NpgsqlParameter[]>()))
                .Returns(Task.CompletedTask);

            // Act
            await card.AddCardAsync(card, "Host=localhost;Username=postgres;Password=fhtw;Database=mtcg;Port=5432");

            // Assert
            _mockDbConnection.Verify(c => c.ExecuteScalarAsync(It.IsAny<string>(), It.IsAny<NpgsqlParameter[]>()), Times.Once);
            _mockDbConnection.Verify(c => c.ExecuteNonQueryAsync(It.IsAny<string>(), It.IsAny<NpgsqlParameter[]>()), Times.Exactly(2));
        }
    }
}

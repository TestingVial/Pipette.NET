# Board-game domain

This project is a small, persistence-free domain used to demonstrate unit and integration tests. It does not access a database, filesystem, network, or other external system.

## Entities

- `User` represents a player with a stable identifier and display name.
- `Game` represents a board game and its supported player-count range.
- `GameCollection` represents the games owned by one user and prevents duplicate games.
- `Play` records a game, its participants, and when it was played.

## Services

- `GameCollectionService` manages in-memory collections and answers ownership queries.
- `PlayService` validates player counts, rejects duplicate participants, and keeps an in-memory play history.

The services intentionally keep their state in memory so tests can exercise the domain without provisioning external infrastructure.

## Class diagram

```mermaid
classDiagram
	class User {
		+Guid Id
		+string Name
	}
	class Game {
		+Guid Id
		+string Title
		+int MinimumPlayers
		+int MaximumPlayers
	}
	class GameCollection {
		+Guid OwnerId
		+IReadOnlyCollection~Game~ Games
		+Add(Game game)
		+Contains(Guid gameId) bool
	}
	class Play {
		+Guid Id
		+Game Game
		+DateTimeOffset PlayedAt
		+IReadOnlyCollection~User~ Participants
	}
	class GameCollectionService {
		+GetCollection(User user) GameCollection
		+AddGame(User user, Game game)
		+OwnsGame(User user, Guid gameId) bool
	}
	class PlayService {
		+IReadOnlyCollection~Play~ Plays
		+RecordPlay(Game game, IEnumerable~User~ participants, DateTimeOffset playedAt) Play
	}
	User "1" --> "1" GameCollection : owns
	GameCollection "1" o-- "0..*" Game : contains
	Play "1" --> "1" Game : records
	Play "1" o-- "1..*" User : participants
	GameCollectionService ..> GameCollection : manages
	PlayService ..> Play : creates
```

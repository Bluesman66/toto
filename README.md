Проект "Список дел" представляет из себя WinForms приложение

Использованы архитектурные и дизайн-паттерны: Clean Architecture, Repository, UnitOfWork, CQRS, MVP, async/await

Стартовый проект Todo.Presentation

Структура БД:

CREATE TABLE IF NOT EXISTS Categories (
Id TEXT PRIMARY KEY,
Name TEXT NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS TodoItems (
Id TEXT PRIMARY KEY,
Title TEXT NOT NULL,
Description TEXT,
CategoryId TEXT NOT NULL REFERENCES Categories(Id),
Priority INTEGER NOT NULL,
DueDate TEXT,
Status INTEGER NOT NULL,
CreatedAt TEXT NOT NULL
);

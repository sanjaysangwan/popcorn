--
-- Popcorn - family movie ratings
-- Microsoft Access (Jet / ACE) schema.
--
-- Setup.aspx runs these statements for you. This file is here so you can
-- build the database by hand instead: create a blank database in Access, open
-- the SQL view of a new query, and run each statement below on its own -
-- Access executes one statement per query, so they cannot be pasted in as a
-- single block.
--
-- Keep this file in step with the Ddl array in App_Code/DatabaseInstaller.cs.
--

CREATE TABLE Users (
    UserId          AUTOINCREMENT PRIMARY KEY,
    Email           TEXT(150) NOT NULL,
    DisplayName     TEXT(80)  NOT NULL,
    PasswordHash    TEXT(120) NOT NULL,
    PasswordSalt    TEXT(60)  NOT NULL,
    IsApproved      YESNO     NOT NULL,
    IsAdmin         YESNO     NOT NULL,
    IsDisabled      YESNO     NOT NULL,
    CreatedUtc      DATETIME  NOT NULL,
    ApprovedUtc     DATETIME,
    LastLoginUtc    DATETIME
);

CREATE UNIQUE INDEX IX_Users_Email ON Users (Email);

CREATE TABLE Movies (
    MovieId         AUTOINCREMENT PRIMARY KEY,
    Title           TEXT(200) NOT NULL,
    ReleaseYear     TEXT(12),
    ImdbId          TEXT(20),
    PosterUrl       TEXT(255),
    Plot            MEMO,
    Actors          TEXT(255),
    Director        TEXT(255),
    Genre           TEXT(150),
    Runtime         TEXT(40),
    MpaaRating      TEXT(20),
    ImdbScore       TEXT(10),
    AddedByUserId   LONG      NOT NULL,
    AddedUtc        DATETIME  NOT NULL
);

CREATE INDEX IX_Movies_Added ON Movies (AddedUtc);

CREATE TABLE Ratings (
    RatingId        AUTOINCREMENT PRIMARY KEY,
    MovieId         LONG      NOT NULL,
    UserId          LONG      NOT NULL,
    Stars           LONG      NOT NULL,
    Review          MEMO,
    CreatedUtc      DATETIME  NOT NULL,
    UpdatedUtc      DATETIME  NOT NULL
);

-- One rating per family member per movie. The headline score for a film is
-- always AVG(Stars) over these rows - it is never stored anywhere.
CREATE UNIQUE INDEX IX_Ratings_MovieUser ON Ratings (MovieId, UserId);

--/* ============================================================
--   RaceDay Event Management System
--   Section C - SQL Database Script
--   ============================================================ */


--/* ============================================================
--   1. CREATE DATABASE
--   ============================================================ */

--CREATE DATABASE RaceDayDB;

--USE RaceDayDB;

--/* ============================================================
--   2. CREATE ORGANIZER TABLE
--   ============================================================ */

--CREATE TABLE ORGANIZER
--(
--    OrganizerID INT IDENTITY(1,1) PRIMARY KEY,
--    FirstName VARCHAR(50) NOT NULL,
--    LastName VARCHAR(50) NOT NULL,
--    Email VARCHAR(100) NOT NULL UNIQUE,
--    PasswordHash VARCHAR(255) NOT NULL
--);


--/* ============================================================
--   3. CREATE PARTICIPANT TABLE
--   ============================================================ */

--CREATE TABLE PARTICIPANT
--(
--    ParticipantID INT IDENTITY(1,1) PRIMARY KEY,
--    FirstName VARCHAR(50) NOT NULL,
--    LastName VARCHAR(50) NOT NULL,
--    Email VARCHAR(100) NOT NULL UNIQUE,
--    PasswordHash VARCHAR(255) NOT NULL
--);


--/* ============================================================
--   4. CREATE EVENT TABLE
--   ============================================================ */

--CREATE TABLE EVENT
--(
--    EventID INT IDENTITY(1,1) PRIMARY KEY,
--    EventName VARCHAR(100) NOT NULL,
--    Description VARCHAR(500) NOT NULL,
--    EventDate DATE NOT NULL,
--    Location VARCHAR(150) NOT NULL,
--    Distance DECIMAL(6,2) NOT NULL,
--    EventType VARCHAR(20) NOT NULL,
--    OrganizerID INT NOT NULL,

--    CONSTRAINT CK_EVENT_Distance
--        CHECK (Distance > 0),

--    CONSTRAINT CK_EVENT_EventType
--        CHECK (EventType IN ('Run', 'Walk', 'Cycle')),

--    CONSTRAINT FK_EVENT_Organizer
--        FOREIGN KEY (OrganizerID)
--        REFERENCES ORGANIZER(OrganizerID)
--);


--/* ============================================================
--   5. CREATE CATEGORY TABLE
--   ============================================================ */

--CREATE TABLE CATEGORY
--(
--    CategoryID INT IDENTITY(1,1) PRIMARY KEY,
--    CategoryName VARCHAR(60) NOT NULL,
--    CategoryType VARCHAR(20) NOT NULL,
--    EventID INT NOT NULL,

--    CONSTRAINT CK_CATEGORY_CategoryType
--        CHECK (CategoryType IN ('Age', 'Distance')),

--    CONSTRAINT FK_CATEGORY_Event
--        FOREIGN KEY (EventID)
--        REFERENCES EVENT(EventID)
--);


--/* ============================================================
--   6. CREATE ENROLMENT TABLE
--   ============================================================ */

--CREATE TABLE ENROLMENT
--(
--    EnrolmentID INT IDENTITY(1,1) PRIMARY KEY,
--    EnrolmentDate DATE NOT NULL
--        DEFAULT GETDATE(),
--    Status VARCHAR(20) NOT NULL
--        DEFAULT 'Confirmed',
--    ParticipantID INT NOT NULL,
--    EventID INT NOT NULL,
--    CategoryID INT NOT NULL,

--    CONSTRAINT CK_ENROLMENT_Status
--        CHECK (Status IN ('Pending', 'Confirmed', 'Cancelled')),

--    CONSTRAINT FK_ENROLMENT_Participant
--        FOREIGN KEY (ParticipantID)
--        REFERENCES PARTICIPANT(ParticipantID),

--    CONSTRAINT FK_ENROLMENT_Event
--        FOREIGN KEY (EventID)
--        REFERENCES EVENT(EventID),

--    CONSTRAINT FK_ENROLMENT_Category
--        FOREIGN KEY (CategoryID)
--        REFERENCES CATEGORY(CategoryID),

--    CONSTRAINT UQ_ENROLMENT_Participant_Event
--        UNIQUE (ParticipantID, EventID)
--);


--/* ============================================================
--   7. CREATE RESULT TABLE
--   ============================================================ */

--CREATE TABLE RESULT
--(
--    ResultID INT IDENTITY(1,1) PRIMARY KEY,
--    FinishTime TIME NOT NULL,
--    Position INT NOT NULL,
--    EnrolmentID INT NOT NULL UNIQUE,

--    CONSTRAINT CK_RESULT_Position
--        CHECK (Position > 0),

--    CONSTRAINT FK_RESULT_Enrolment
--        FOREIGN KEY (EnrolmentID)
--        REFERENCES ENROLMENT(EnrolmentID)
--);


--/* ============================================================
--   8. INSERT ORGANISERS
--   Minimum required: 2
--   ============================================================ */

--INSERT INTO ORGANIZER
--    (FirstName, LastName, Email, PasswordHash)
--VALUES
--    ('Thabo', 'Wilson', 'thabo.wilson@gmail.com', 'hashed_password_001'),
--    ('Sarah', 'Jones', 'srah_jones@gmail.com', 'hashed_password_002');


--Verification query

--SELECT *
--FROM ORGANIZER;

--/* ============================================================
--   9. INSERT PARTICIPANTS
--   Minimum required: 2
--   ============================================================ */

--INSERT INTO PARTICIPANT
--    (FirstName, LastName, Email, PasswordHash)
--VALUES
--    ('Jessica', 'Woods', 'jess.woodz@outlook.com', 'hashed_password_003'),
--    ('Lerato', 'Dlamini', 'lerato.dlamini@gmail.com', 'hashed_password_004');


--Verification query

--SELECT *
--FROM PARTICIPANT;

--/* ============================================================
--   10. INSERT EVENTS
--   Minimum required: 3
--   ============================================================ */

--INSERT INTO EVENT
--    (EventName, Description, EventDate, Location, Distance, EventType, OrganizerID)
--VALUES
--    (
--        'Pretoria City Run',
--        'A 10 kilometre road running event through Pretoria.',
--        '2026-11-10',
--        'Pretoria',
--        10.00,
--        'Run',
--        1
--    ),
--    (
--        'Tshwane Family Walk',
--        'A community walking event suitable for families.',
--        '2026-10-17',
--        'Tshwane',
--        5.00,
--        'Walk',
--        1
--    ),
--    (
--        'Gauteng Cycle Challenge',
--        'A competitive cycling event for recreational and experienced cyclists.',
--        '2026-11-07',
--        'Johannesburg',
--        40.00,
--        'Cycle',
--        2
--    );


--Verification query

--SELECT *
--FROM EVENT;

--/* ============================================================
--   11. INSERT CATEGORIES
--   Each event receives multiple categories.
--   ============================================================ */

--INSERT INTO CATEGORY
--    (CategoryName, CategoryType, EventID)
--VALUES
--    ('Under 20', 'Age', 1),
--    ('Senior', 'Age', 1),
--    ('10km', 'Distance', 1),

--    ('Under 20', 'Age', 2),
--    ('Senior', 'Age', 2),
--    ('5km', 'Distance', 2),

--    ('Junior', 'Age', 3),
--    ('Senior', 'Age', 3),
--    ('40km', 'Distance', 3);


--Verification query

--SELECT
--    C.CategoryID,
--    C.CategoryName,
--    C.CategoryType,
--    E.EventName
--FROM CATEGORY C
--INNER JOIN EVENT E
--    ON C.EventID = E.EventID;

--/* ============================================================
--   12. INSERT ENROLMENTS
--   ============================================================ */

--INSERT INTO ENROLMENT
--    (EnrolmentDate, Status, ParticipantID, EventID, CategoryID)
--VALUES
--    ('2026-09-01', 'Confirmed', 1, 1, 1),
--    ('2026-09-02', 'Confirmed', 2, 1, 2),
--    ('2026-09-03', 'Confirmed', 1, 2, 4),
--    ('2026-09-04', 'Confirmed', 2, 2, 5),
--    ('2026-09-05', 'Confirmed', 1, 3, 7),
--    ('2026-09-06', 'Confirmed', 2, 3, 8);


--Verification query

--SELECT
--    EN.EnrolmentID,
--    P.FirstName + ' ' + P.LastName AS Participant,
--    E.EventName,
--    C.CategoryName,
--    EN.Status
--FROM ENROLMENT EN
--INNER JOIN PARTICIPANT P
--    ON EN.ParticipantID = P.ParticipantID
--INNER JOIN EVENT E
--    ON EN.EventID = E.EventID
--INNER JOIN CATEGORY C
--    ON EN.CategoryID = C.CategoryID;

--/* ============================================================
--   13. INSERT RESULTS
--   ============================================================ */

--INSERT INTO RESULT
--    (FinishTime, Position, EnrolmentID)
--VALUES
--    ('00:48:35', 1, 1),
--    ('00:52:14', 2, 2),
--    ('01:05:20', 1, 3),
--    ('01:12:45', 2, 4);


--Verification query

--SELECT
--    R.ResultID,
--    P.FirstName + ' ' + P.LastName AS Participant,
--    E.EventName,
--    R.FinishTime,
--    R.Position
--FROM RESULT R
--INNER JOIN ENROLMENT EN
--    ON R.EnrolmentID = EN.EnrolmentID
--INNER JOIN PARTICIPANT P
--    ON EN.ParticipantID = P.ParticipantID
--INNER JOIN EVENT E
--    ON EN.EventID = E.EventID;



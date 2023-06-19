CREATE TABLE Employees (
    Worker_id INT IDENTITY(1,1) PRIMARY KEY NOT NULL,
    Nr_tel CHAR(12) NOT NULL
);

CREATE TABLE Jobs (
    Job_id INT IDENTITY(1,1) PRIMARY KEY NOT NULL,
    Rate_Per_Hour DECIMAL(6,2) NOT NULL,
    Execution_date DATETIME NOT NULL,
    Worker_id INT NOT NULL,
    FOREIGN KEY (Worker_id) REFERENCES Employees (Worker_id)
);
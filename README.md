A small SQLite clone in C#. Based on [Let's Build a Simple Database](https://cstack.github.io/db_tutorial/).
 
Everything lives in one file, stored as a B-tree of 4 KB pages.
 
## Setup
 
Needs the .NET 10. If you use Nix, the flake has it:
 
```
nix develop
```
 
(or `direnv allow`, there's an `.envrc`)
 
## Run
 
```
dotnet run -- mydb.db
```
 
The file name is required. It's created if it doesn't exist.
 
## Commands
 
```
insert 1 alice alice@example.com
select
.btree
.constants
.exit
```
 
Rows are `id username email`, no spaces inside values. Ids are unique and `select` returns rows sorted by id. Username is max 32 bytes, email max 255.
 
Data is only written to disk on `.exit`, so use it.
 
## Tests
 
```
dotnet test database.tests
```


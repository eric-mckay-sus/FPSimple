# Foolproof Sample System demo

## Pre-demo

- Run resetDbEnv.bat
- Copy actual tables to temporaries, delete from actuals

## Demo structure

General flow of the application:

1. Model-line mappings
2. Foolproof data sheets
3. Sample creation
4. Sample print
5. Sample approval
6. Sample remake request
7. Sample remake approval

## Part 0: Using the Application

The app is published to a single .exe file, but it needs all the contents of the /publish directory present to operate correctly.  

Need some environment variables for semi-secure storage. [Show message from trying to run without environment variables]

- Run loadDbEnv.bat (app loads)

Mention auto-login borrowing Windows credentials (don't have an approver logged in on a machine on the floor used by other associates). More on this later

## Part 1: Empty System Demo

### Phase 0: Show Empty State

#### What to Show

- No mappings
- No data sheets
- Empty sample table
- No printable samples
- No approval tasks
- No remake requests

### Key Talking Points

- System is workflow-driven
- Nothing downstream can be used until prerequisite data exists

### Important Visuals

- Empty tables
- Empty dashboards
- Zero counts

## Phase 1: Model Mapping Uploader

### Goal

Connect models to lines in order to enforce only valid model-line pairings in sample creation.

### Demonstrate

Show contents, then upload tooSmall.csv. It will run into a SQL error because CsvHelper expects a column where there is none.  

Show contents, then upload tooBig.csv. It will not run into any errors because CsvHelper has everything it wants and simply ignores the extraneous data.  

Point out how there can be total garbage data in any column and as long as there's not too many characters, the uploader does not care (it has nometric to grade good/bad input)
Show contents, then upload tooLong.csv. It will run into the column length SQL restriction. Note that this limit is simply inherited from the C. Core DB and can be modified here independently.

Show contents, then upload testModelMappings.csv. It will work perfectly. Explain that it's designed to work with the query on C. Core (Nick wrote), but anything with five columns, headers, and the right length of data will work (future-proof against moving away from C.Core).

### What to Highlight

- Validation during upload
- Parsing behavior
- Mapping persistence
- Immediate database population

### Show Resulting Effects

After upload:

- mappings table populated
- models become selectable elsewhere
- downstream workflows unlock

### Good Demo Moment

Show:

- before upload → no selectable models
- after upload → models immediately available

---

## Phase 2 — Foolproof Data Sheet Upload

### Goal

Show enrichment/configuration layer.

### Demonstrate

Upload:

- product/specification sheet
- associated metadata

### Highlight

- linkage to model mappings
- parsing and transformation
- validation/error handling
- deduplication logic if available

### Show Immediate Effects

- records appear in data sheet tables
- model relationships become visible
- sample creation becomes enabled

## Phase 3 — Sample Creation

### Goal

Show operational generation workflow.

This is one of the most important demo sections.

#### Demonstrate

Create:

- one or more samples
- ideally using:

  - mapped model
  - uploaded data sheet
  - configurable parameters

### Highlight

- automatic field population
- generated identifiers
- workflow status initialization
- validation rules
- business logic enforcement

### Important Database Effects to Show

New records:

- sample header
- sample details
- workflow state
- print queue entries (if applicable)

### Good Demo Angle

Show:

- manual entry reduction
- standardized generation
- traceability from origin data

## Phase 4 — Sample Print

### Goal

Show operational execution layer.

### Demonstrate

- selecting created samples
- generating printable output
- print queue/status updates

### Highlight

- print formatting
- barcode/QR support if applicable
- audit trail
- print timestamps/user attribution

### Immediate Effects to Show

- sample status changes
- print records created
- queue updates

## Phase 5 — Sample Approval

### Goal

Show governance + controlled workflow progression.

### Demonstrate

Approve:

- one pending sample

### Highlight

- reviewer workflow
- approval state transition
- audit logging
- permissions/roles if available

### Important State Changes

Show:

- pending → approved
- approval timestamps
- approver attribution
- locked/edit-restricted states

## Phase 6 — Sample Remake Request

### Goal

Show exception handling workflow.

This is important because it proves the system handles real-world operational problems.

### Demonstrate

Request remake for:

- approved sample
- printed sample

### Highlight

- reason capture
- traceability to original sample
- workflow branching
- status escalation

### Important Effects

Show:

- remake request record
- linked parent sample
- new pending approval state

---

## Phase 7 — Sample Remake Approval

### Goal

Close the lifecycle loop.

### Demonstrate

Approve remake.

### Highlight

- controlled regeneration
- lineage tracking
- audit continuity
- distinction between original and remake

### Show

- remake chain/history
- linked records
- new print eligibility

## Transition to “At Scale” Demo

After the clean workflow demo:

> “Now that you’ve seen the lifecycle clearly, let’s look at the system operating with realistic production-scale data.”

This transition is critical.

---

## Part 2 — Populated / Scale Demo

Goal:
Show operational maturity.

This portion should emphasize:

- searchability,
- throughput,
- visibility,
- governance,
- scalability.

---

## Features to Highlight at Scale

### 1. Search & Filtering

Demonstrate:

- sample ID lookup
- model filtering
- approval status filtering
- remake filtering
- date ranges

### Emphasize

- operational usability
- rapid traceability

## 2. Workflow Queues

Show:

- pending approvals
- pending prints
- remake queues

Highlight:

- prioritization
- queue management
- operational visibility

## 3. Audit Trails

Show:

- who uploaded mappings
- who approved samples
- remake history
- timestamps
- print history

This is often a major differentiator.

## 4. Relationship Navigation

Demonstrate:

- mapping → data sheet
- data sheet → sample
- sample → print
- sample → approval
- sample → remake lineage

This helps people understand the data model intuitively.

## 5. Error Handling / Validation

- upload malformed mapping
- duplicate upload
- invalid remake request

Then show:

- validation messaging
- prevention logic

## 6. Performance / Volume

Show:

- large tables
- pagination
- fast filtering/search
- batch operations

## 7. Role-Based Behavior (If Available)

Demonstrate:

- creator vs approver
- restricted actions
- approval permissions

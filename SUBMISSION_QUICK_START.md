# AMS Submissions - Quick Start Guide

## Access the Submission System

### Main Pages
| URL | Purpose |
|-----|---------|
| `/submissions` | View all submissions (register/list) |
| `/submissions/new` | Create a new submission |
| `/submissions/{id}` | View submission details |

## Creating a New Submission

### Quick Steps
1. Go to `/submissions`
2. Click **"New Submission"** button
3. Complete 6-step wizard:
   - **Step 1**: Select Account (required)
   - **Step 2**: Select LOB (required)
   - **Step 3**: Set Dates & Priority (required)
   - **Step 4**: Select Markets (optional)
   - **Step 5**: Upload Documents (optional)
   - **Step 6**: Review & Submit
4. Click **"Create Submission"** - submission is created and you're redirected to detail page

### What Happens Behind the Scenes
- Submission record created in database
- Unique submission number auto-generated
- Status set to "New"
- Submission added to register
- Workflow can be initiated for market distribution

## Submission Statuses
- 🆕 **New** - Just created, awaiting action
- 📋 **In Review** - Markets reviewing submission
- 💬 **Quoted** - Quotes received from markets
- ✅ **Bound** - Coverage bound with carrier
- ❌ **Declined** - All markets declined
- 🔒 **Closed** - Submission archived

## Submission Detail View

### Information Displayed
- Submission number and account name
- LOB, dates, priority, producer
- Current status and workflow stage
- Related applications, quotes, declines
- Activity timeline and history

### Tabs Available
- **Overview** - Main submission details
- **Applications** - Carrier applications sent
- **Quotes** - Quotes received from markets
- **Declines** - Declined quotes
- **Timeline** - Activity and status history

## Searching & Filtering

### From Submissions Register
- **Search Box**: Enter submission number, account name, or LOB
- **Filter Button**: Advanced filtering by status, date, carrier, priority
- **Sort**: Click column headers to sort

### Example Searches
- Search "Sullivan" → finds all Sullivan Mfg submissions
- Search "GL" → finds General Liability submissions
- Filter by status "Quoted" → see all quoted submissions

## Key Features

✅ **Submission Creation**
- Guided 6-step wizard
- Real-time validation
- Save as draft functionality
- Auto-generated submission numbers

✅ **Submission Management**
- View all submissions in one place
- Advanced filtering and search
- Status tracking
- Activity timeline

✅ **Market Distribution** (Ready for implementation)
- Send submissions to multiple markets
- Track market responses
- Manage quotes and declines

✅ **Reporting** (Ready for implementation)
- Submission metrics and KPIs
- Status distribution
- Performance analytics

## API Integration

### Create Submission
```
POST /api/submissions
Content-Type: application/json

{
  "tenantId": "00000000-0000-0000-0000-000000000001",
  "accountId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "lineOfBusiness": "Commercial GL",
  "priority": "High",
  "effectiveDate": "2026-05-22",
  "expirationDate": "2027-05-22",
  "targetPremium": 50000,
  "assignedToUserId": null
}

Response: { "id": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" }
```

### Search Submissions
```
GET /api/submissions?searchTerm=sullivan&pageNumber=1&pageSize=25

Response: {
  "items": [...],
  "totalCount": 5,
  "pageNumber": 1,
  "pageSize": 25
}
```

### Get Submission Detail
```
GET /api/submissions/{submissionId}

Response: {
  "submissionId": "...",
  "accountId": "...",
  "submissionNumber": "SUB-2026-001",
  "status": "New",
  ...
}
```

## Troubleshooting

### Can't create submission?
1. Ensure account is selected in Step 1
2. Ensure LOB is selected in Step 2
3. Ensure dates and priority set in Step 3
4. Check browser console for errors

### Submission not appearing in register?
1. Refresh the page
2. Check the submission number in the detail view
3. Try searching for the account name

### Navigation issues?
1. Clear browser cache
2. Check URL: `/submissions` (no trailing slash)
3. Verify authentication/permissions

## Developer Notes

### Adding New Fields to Submission
1. Update `CreateSubmissionRequest` in `SubmissionRequests.cs`
2. Update `SubmissionDto` in DTOs
3. Update wizard steps in `NewSubmissionWizard.razor`
4. Update database schema via migrations

### Database Schema
- **Table**: `CRM.Submission`
- **Primary Key**: `SubmissionId` (GUID)
- **Unique**: `SubmissionNumber` (auto-generated)
- **Tracking**: `CreatedDateUtc`, `ModifiedDateUtc`

### Frontend Architecture
- **Pages**: Blazor components in `/Components/Pages/`
- **Services**: `ApiClient` in `/Services/`
- **API Integration**: RESTful via `HttpClient`

### Backend Architecture
- **Controller**: `SubmissionsController.cs`
- **Service**: `SubmissionService.cs`
- **Repository**: `SubmissionRepository.cs`
- **ORM**: Dapper with SQL Server

---

**Version**: 1.0  
**Last Updated**: 2026-04-25  
**Status**: ✅ Production Ready

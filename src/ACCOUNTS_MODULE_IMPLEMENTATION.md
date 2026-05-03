# Accounts Module Implementation Guide

## Overview
This document describes the complete implementation of the **Accounts & Customers** module for the AMS Enterprise system. The module provides comprehensive account lifecycle management, contact relationships, and organizational hierarchy tracking.

## Components Implemented

### 1. **Razor Pages Created**

#### 1.1 Account Creation Page
- **Route**: `/accounts/new`
- **File**: `src\Ams.Web\Components\Pages\Accounts\AccountNew.razor`
- **Features**:
  - Form-based account creation
  - Account information (name, number, type, status)
  - Contact information (email, phone, address)
  - Financial information (revenue, tax ID)
  - Dropdown selections for account types, statuses, and segments
  - Form validation and toast notifications
  - Success redirect to account detail view

#### 1.2 Account Hierarchy Page
- **Route**: `/accounts/{accountId:guid}/hierarchy`
- **File**: `src\Ams.Web\Components\Pages\Accounts\AccountHierarchy.razor`
- **Features**:
  - Display parent account relationships
  - List all child accounts
  - Link/unlink child accounts
  - Visual hierarchy tree representation
  - Add child account modal dialog
  - Search functionality for account selection
  - Breadcrumb navigation

#### 1.3 Account Timeline Page
- **Route**: `/accounts/{accountId:guid}/timeline`
- **File**: `src\Ams.Web\Components\Pages\Accounts\AccountTimeline.razor`
- **Features**:
  - Chronological activity timeline
  - Event filtering by type (Account Changes, Contacts Added, Opportunities, etc.)
  - Timeline visualization with event dots
  - Event details (title, description, timestamp, creator)
  - Related entity tracking
  - Pagination with "Load More" functionality
  - Event type indicators with color coding

#### 1.4 Account Relationships Page
- **Route**: `/accounts/{accountId:guid}/relationships`
- **File**: `src\Ams.Web\Components\Pages\Accounts\AccountRelationships.razor`
- **Features**:
  - Related accounts management (Partner, Subsidiary, Affiliate, etc.)
  - Key contacts display with contact information
  - Service providers listing
  - Active opportunities display
  - Add relationship modal with account search
  - Remove relationships with confirmation
  - Contact communication links (email, phone, LinkedIn)

#### 1.5 Contact Roles Page
- **Route**: `/client/contacts/roles`
- **File**: `src\Ams.Web\Components\Pages\Accounts\ContactRoles.razor`
- **Features**:
  - List all defined contact roles
  - Display role descriptions and contact count
  - Create new contact roles
  - Set default role for new contacts
  - Grid-based role cards
  - KPI strip showing total roles

#### 1.6 Decision Makers Page
- **Route**: `/client/contacts/dm`
- **File**: `src\Ams.Web\Components\Pages\Accounts\DecisionMakers.razor`
- **Features**:
  - Filterable list of decision makers
  - Filter by industry
  - Contact information display
  - Links to email, phone, and LinkedIn profiles
  - Account association tracking
  - Last contact date display
  - Pagination support
  - KPI metrics (total decision makers, accounts, email availability)

### 2. **Styling**

#### 2.1 Comprehensive CSS Stylesheet
- **File**: `src\Ams.Web\Components\Pages\Accounts\Accounts.razor.css`
- **Features**:
  - Responsive grid layouts
  - Modal dialogs and overlays
  - Form styling with focus states
  - Card-based UI components
  - Timeline visualization
  - Status badges with color coding
  - KPI cards
  - Mobile-responsive breakpoints
  - Accessibility-focused styling
  - Smooth transitions and hover effects

### 3. **Database Schema**

#### 3.1 Migration Script
- **File**: `src\Ams.Infrastructure\Migrations\0046_AccountsLifecycleSchema.sql`
- **Tables Created**:

##### a. **Client.AccountRelationship**
- Primary Keys: RelationshipId
- Foreign Keys: SourceAccountId, RelatedAccountId
- Columns:
  - RelationshipType (Parent, Subsidiary, Partner, Affiliate, etc.)
  - IsActive, Description
  - Audit fields (CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, etc.)
- Indexes:
  - TenantId, IsDeleted
  - SourceAccountId, RelatedAccountId

##### b. **Client.ContactRole**
- Primary Keys: ContactRoleId
- Columns:
  - RoleName, Description
  - IsActive, IsDefault
  - Audit fields
- Indexes:
  - TenantId, IsDeleted

##### c. **Client.AccountHierarchy**
- Primary Keys: HierarchyId
- Foreign Keys: ParentAccountId, ChildAccountId
- Columns:
  - HierarchyLevel
  - IsActive
  - Audit fields
- Indexes:
  - Unique index on (ParentAccountId, ChildAccountId, TenantId)
  - TenantId, ParentAccountId, ChildAccountId

##### d. **Client.AccountActivity**
- Primary Keys: ActivityId
- Foreign Keys: AccountId
- Columns:
  - ActivityType (AccountChange, ContactAdded, Opportunity, etc.)
  - Title, Description
  - RelatedEntityType, RelatedEntityId
  - Metadata (JSON)
  - Audit fields
- Indexes:
  - AccountId with CreatedDateUtc DESC (for timeline queries)
  - CreatedDateUtc DESC (for recent activities)

##### e. **Client.AccountServiceProvider**
- Primary Keys: ProviderId
- Foreign Keys: AccountId
- Columns:
  - ProviderName, ServiceType
  - ContactName, ContactEmail, ContactPhone
  - IsActive
  - Audit fields
- Indexes:
  - TenantId, AccountId

##### f. **Client.AccountExtended**
- Primary Keys: AccountExtendedId
- Foreign Keys: AccountId
- Columns:
  - ParentAccountId, OwnerUserId
  - LifecycleStage, Segment, Industry
  - NumberOfEmployees, AnnualRevenue, Website, TaxId, NaicsCode
  - RenewalRisk, ChurnRisk, HealthScore
  - TotalPremium, OpenClaims, ActivePolicies, OpenOpportunities
  - FirstPolicyDate, LastActivityDate
  - Audit fields
- Indexes:
  - TenantId, Segment, RenewalRisk (for filtering)
  - OwnerUserId (for user-scoped queries)

#### 3.2 Seed Data
- Default Contact Roles:
  - Decision Maker (default)
  - Finance Contact
  - Operations Contact
  - HR Contact

### 4. **Navigation Integration**

The following routes have been added to the NavSidebar component:

```
4. Accounts & Customers (accounts-lifecycle)
   ├── Account Hub (accounts-core)
   │   ├── Accounts Dashboard → /accounts
   │   ├── Account Directory → /accounts
   │   ├── New Account → /accounts/new
   │   ├── Account 360 → /accounts/{id}
   │   ├── Account Hierarchy → /accounts/{id}/hierarchy
   │   ├── Account Timeline → /accounts/{id}/timeline
   │   └── Account Relationships → /accounts/{id}/relationships
   │
   ├── Contacts & Relationships (contacts-core)
   │   ├── Contact Directory → /client/contacts
   │   ├── New Contact → /client/contacts/new
   │   ├── Contact 360 → /client/contacts/{id}
   │   ├── Contact Roles → /client/contacts/roles
   │   ├── Decision Makers → /client/contacts/dm
   │   └── Contact Timeline → /client/contacts/{id}/timeline
   │
   └── Account Intelligence & Notes (account-intel)
       ├── Account Notes → /client/account-notes
       ├── Portal Invites → /client/portal-invites
       ├── Account Ownership → /client/account-ownership
       ├── Account Segments → /client/segments
       ├── Account Events → /client/account-events
       └── Account Health → /client/account-health
```

## API Integration Points

The pages integrate with the following API endpoints (to be implemented):

### Account Endpoints
- `POST /api/accounts` - Create new account
- `GET /api/accounts/{id}` - Get account details
- `GET /api/accounts/{id}/hierarchy` - Get account hierarchy
- `POST /api/accounts/{id}/children` - Link child account
- `DELETE /api/accounts/{id}/children/{childId}` - Unlink child account
- `GET /api/accounts/{id}/timeline` - Get activity timeline
- `GET /api/accounts/{id}/relationships` - Get account relationships
- `POST /api/accounts/{id}/relationships` - Add relationship
- `DELETE /api/accounts/{id}/relationships/{relationshipId}` - Remove relationship

### Contact Endpoints
- `GET /api/contacts/roles` - List all contact roles
- `POST /api/contacts/roles` - Create contact role
- `GET /api/contacts/decision-makers` - List decision makers

## Data Models

### AccountFormModel
Used for account creation:
- AccountNumber (optional, auto-generated)
- AccountName (required)
- AccountTypeCode (required)
- StatusCode (default: "Active")
- SegmentCode (optional)
- Industry (optional)
- MainEmail, MainPhone (optional)
- Website (optional)
- Address fields (optional)
- AnnualRevenue, TaxId (optional)

### AccountHierarchyData
Represents account hierarchy structure:
- AccountId, AccountName, AccountNumber
- AccountType, Status, CreatedDate
- ParentAccounts: List of parent relationships
- ChildAccounts: List of child relationships

### TimelineEvent
Activity log entry:
- EventId, Title, Description
- EventType (AccountChange, ContactAdded, etc.)
- RelatedEntityType, RelatedEntityId
- CreatedDate, CreatedByUserName

### AccountRelationshipsData
Account relationships container:
- RelatedAccounts: List of related accounts
- Contacts: List of key contacts
- ServiceProviders: List of service providers
- Opportunities: List of active opportunities

## Styling Architecture

### CSS Variables Used
- `--um-primary`, `--um-primary-alpha`, `--um-primary-dark`
- `--um-secondary`, `--um-secondary-alpha`
- `--um-success`, `--um-success-alpha`
- `--um-danger`, `--um-danger-alpha`
- `--um-warning`, `--um-warning-alpha`
- `--um-info`, `--um-info-alpha`
- `--um-text-primary`, `--um-text-secondary`, `--um-text-disabled`
- `--um-bg-card`, `--um-bg-input`, `--um-bg-disabled`
- `--um-border-color`
- `--um-radius-sm`, `--um-radius-md`
- `--um-shadow-sm`

### Responsive Breakpoints
- Tablet and below: 768px
- Grid layouts collapse to single column
- Modals adjust width to 95% on mobile
- Pagination stack vertically on mobile

## Implementation Checklist

### Database
- [x] Create migration script
- [x] Define all tables with relationships
- [x] Add indexes for performance
- [x] Seed default contact roles
- [x] Add foreign key constraints

### Frontend Components
- [x] Account creation page
- [x] Account hierarchy page
- [x] Account timeline page
- [x] Account relationships page
- [x] Contact roles page
- [x] Decision makers page
- [x] Comprehensive CSS styling
- [x] Responsive design

### Navigation
- [x] Updated NavSidebar.razor with all routes
- [x] Breadcrumb integration
- [x] Icon assignments

### Still Required
- [ ] API Controllers (AccountsController, ContactsController)
- [ ] Service layer (AccountService, ContactService)
- [ ] Repository implementations (AccountRepository extensions)
- [ ] Unit tests
- [ ] API documentation
- [ ] Performance optimization queries

## Usage Examples

### Creating an Account
1. Navigate to `/accounts/new`
2. Fill in required fields (Account Name, Type)
3. Add optional information (email, phone, address)
4. Click "Create Account"
5. Automatically redirected to account detail view

### Managing Account Hierarchy
1. Navigate to `/accounts/{id}/hierarchy`
2. View parent and child accounts
3. Click "Link Child Account" button
4. Search and select a child account
5. Save relationship

### Viewing Account Timeline
1. Navigate to `/accounts/{id}/timeline`
2. See chronological list of activities
3. Filter by activity type using dropdown
4. View event details and related entities
5. Load more events with "Load More" button

### Managing Decision Makers
1. Navigate to `/client/contacts/dm`
2. View all decision makers with contact info
3. Filter by industry using dropdown
4. Click contact name to view profile
5. Use email/phone/LinkedIn links to reach out

## Performance Considerations

- Account queries use indexed columns (TenantId, AccountId, IsDeleted)
- Timeline uses CreatedDateUtc index for efficient sorting
- Relationship queries use compound indexes for better query plans
- Pagination implemented for large datasets
- Activity table can be archived periodically for old events

## Security Considerations

- All queries filtered by TenantId for multi-tenancy
- User ID tracked for audit trail
- IsDeleted soft delete pattern prevents accidental data loss
- Breadcrumb navigation maintains user context
- Modal dialogs prevent accidental navigation away

## Future Enhancements

1. Real-time notifications when accounts are modified
2. Account scoring algorithm for customer health
3. Bulk account operations and imports
4. Advanced filtering and saved views
5. Account relationship analytics
6. Integration with external data sources
7. Account merge functionality
8. Custom field support for extensibility
9. Activity feed subscription/notifications
10. Account export functionality

## Support & Troubleshooting

### Common Issues

**Issue**: Page shows "Account not found"
- **Solution**: Verify account ID in URL exists and user has access

**Issue**: Modal not appearing when clicking "Add" button
- **Solution**: Check browser console for JavaScript errors; ensure Blazor is properly loaded

**Issue**: Relationships not saving
- **Solution**: Verify related account exists; check network tab for API errors

**Issue**: Slow timeline loading
- **Solution**: Consider adding pagination or date range filters; archive old activities

## Related Documentation

- [Navigation Component Guide](../Navigation/NavSidebar.md)
- [API Integration Guide](../API/Integration.md)
- [Database Schema Reference](../Database/Schema.md)
- [Styling Guide](../Styling/ComponentStyles.md)
- [Testing Guide](../Testing/UnitTests.md)

## Questions & Support

For questions or issues related to this implementation:
1. Check this document and related guides
2. Review existing test cases for usage patterns
3. Contact the development team
4. File an issue in the project repository

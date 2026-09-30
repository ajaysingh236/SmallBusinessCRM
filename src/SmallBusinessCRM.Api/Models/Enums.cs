namespace SmallBusinessCRM.Api.Models;
public enum LeadStatus { New, Contacted, DemoScheduled, ProposalSent, ClosedWon, ClosedLost }
public enum ActivityType { Note, Call, Email, Meeting, StatusChanged, System }
public enum EmailTemplateKind { None, MeetingConfirmation, FollowUp, Quotation }

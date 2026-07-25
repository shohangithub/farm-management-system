import { Address } from './organization.model';

export interface Branch {
  id: string;
  organizationId: string;
  branchCode: string;
  name: string;
  managerUserId?: string;
  contactEmail: string;
  contactPhone?: string;
  address?: Address;
  latitude?: number;
  longitude?: number;
  status: number; // enum: 1=Active, 2=Inactive, 3=Closed
  workingHours?: string;
  holidayCalendar?: string;
  isHeadOffice: boolean;
  createdAtUtc?: string;
  createdBy?: string;
  modifiedAtUtc?: string;
  modifiedBy?: string;
}

export interface BranchList {
  id: string;
  branchCode: string;
  name: string;
  contactEmail: string;
  contactPhone?: string;
  status: number;
  isHeadOffice: boolean;
}

export interface CreateBranchCommand {
  organizationId: string;
  branchCode: string;
  name: string;
  contactEmail: string;
  contactPhone?: string;
  street?: string;
  city?: string;
  state?: string;
  country?: string;
  zipCode?: string;
  latitude?: number;
  longitude?: number;
  workingHours?: string;
  holidayCalendar?: string;
  isHeadOffice: boolean;
}

export interface UpdateBranchCommand extends CreateBranchCommand {
  id: string;
}

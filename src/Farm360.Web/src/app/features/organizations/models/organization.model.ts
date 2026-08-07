export interface Address {
  street: string;
  city: string;
  state: string;
  country: string;
  zipCode: string;
}

export interface Organization {
  id: string;
  name: string;
  logoUrl?: string;
  contactEmail: string;
  contactPhone?: string;
  businessRegistrationNumber?: string;
  tradeLicenseNumber?: string;
  taxIdentificationNumber?: string;
  currencyCode: string;
  timeZoneId: string;
  languageCode: string;
  address?: Address;
  businessType: string; // enum BusinessType
  status: string; // enum OrganizationStatus
  createdAt: string;
  createdBy?: string;
  lastModifiedAt?: string;
  lastModifiedBy?: string;
  rowVersion: string; // byte[] represented as base64 or string
}

export interface CreateOrganizationCommand {
  name: string;
  logoUrl?: string;
  contactEmail: string;
  contactPhone?: string;
  businessRegistrationNumber?: string;
  tradeLicenseNumber?: string;
  taxIdentificationNumber?: string;
  currencyCode: string;
  timeZoneId: string;
  languageCode: string;
  street?: string;
  city?: string;
  state?: string;
  country?: string;
  zipCode?: string;
  businessType: string;
}

export interface UpdateOrganizationCommand extends CreateOrganizationCommand {
  id: string;
}

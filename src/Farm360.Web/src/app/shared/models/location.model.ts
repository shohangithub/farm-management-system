export interface Country {
  id: string;
  name: string;
  code: string;
}

export interface Division {
  id: string;
  countryId: string;
  name: string;
}

export interface District {
  id: string;
  divisionId: string;
  name: string;
}

export interface Upazila {
  id: string;
  districtId: string;
  name: string;
}

export interface Union {
  id: string;
  upazilaId: string;
  name: string;
}

export interface Village {
  id: string;
  unionId: string;
  name: string;
}

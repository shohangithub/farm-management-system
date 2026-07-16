import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MasterDataService } from '../../../shared/services/master-data.service';
import { MasterDataEntry, MasterDataType, CreateMasterDataCommand, UpdateMasterDataCommand } from '../../../shared/models/master-data.model';

@Component({
  selector: 'app-master-data',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './master-data.component.html'
})
export class MasterDataComponent implements OnInit {
  private masterDataService = inject(MasterDataService);

  types = [
    { id: MasterDataType.Breed, name: 'Breed' },
    { id: MasterDataType.AnimalType, name: 'Animal Type' },
    { id: MasterDataType.FeedType, name: 'Feed Type' },
    { id: MasterDataType.MedicineType, name: 'Medicine Type' },
    { id: MasterDataType.VaccinationType, name: 'Vaccination Type' },
    { id: MasterDataType.Disease, name: 'Disease' },
    { id: MasterDataType.SupplierCategory, name: 'Supplier Category' },
    { id: MasterDataType.ExpenseCategory, name: 'Expense Category' },
    { id: MasterDataType.PaymentMethod, name: 'Payment Method' },
    { id: MasterDataType.MeasurementUnit, name: 'Measurement Unit' },
    { id: MasterDataType.Currency, name: 'Currency' },
    { id: MasterDataType.Language, name: 'Language' },
    { id: MasterDataType.Timezone, name: 'Timezone' },
    { id: MasterDataType.BusinessType, name: 'Business Type' }
  ];

  selectedType: MasterDataType = MasterDataType.AnimalType;
  entries: MasterDataEntry[] = [];
  isLoading = false;

  // Form State
  isModalOpen = false;
  editingId: string | null = null;
  formData: any = {
    name: '',
    code: '',
    description: '',
    displayOrder: 0,
    isActive: true
  };

  ngOnInit(): void {
    this.loadEntries();
  }

  selectType(type: MasterDataType): void {
    this.selectedType = type;
    this.loadEntries();
  }

  loadEntries(): void {
    this.isLoading = true;
    this.masterDataService.getByType(this.selectedType, true).subscribe({
      next: (data) => {
        this.entries = data;
        this.isLoading = false;
      },
      error: (err) => {
        console.error(err);
        this.isLoading = false;
      }
    });
  }

  openCreateModal(): void {
    this.editingId = null;
    this.formData = { name: '', code: '', description: '', displayOrder: 0, isActive: true };
    this.isModalOpen = true;
  }

  openEditModal(entry: MasterDataEntry): void {
    this.editingId = entry.id;
    this.formData = { ...entry };
    this.isModalOpen = true;
  }

  closeModal(): void {
    this.isModalOpen = false;
  }

  saveEntry(): void {
    if (this.editingId) {
      const command: UpdateMasterDataCommand = {
        id: this.editingId,
        name: this.formData.name,
        description: this.formData.description,
        displayOrder: this.formData.displayOrder,
        isActive: this.formData.isActive
      };
      this.masterDataService.update(this.editingId, command, this.selectedType).subscribe({
        next: () => {
          this.closeModal();
          this.loadEntries();
        }
      });
    } else {
      const command: CreateMasterDataCommand = {
        type: this.selectedType,
        name: this.formData.name,
        code: this.formData.code,
        description: this.formData.description,
        displayOrder: this.formData.displayOrder
      };
      this.masterDataService.create(command).subscribe({
        next: () => {
          this.closeModal();
          this.loadEntries();
        }
      });
    }
  }

  deleteEntry(entry: MasterDataEntry): void {
    if (confirm(`Are you sure you want to delete ${entry.name}?`)) {
      this.masterDataService.delete(entry.id, this.selectedType).subscribe({
        next: () => {
          this.loadEntries();
        }
      });
    }
  }
}

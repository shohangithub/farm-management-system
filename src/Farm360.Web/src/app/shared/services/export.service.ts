import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class ExportService {
  
  exportToCsv(data: any[], filename: string): void {
    if (!data || !data.length) {
      console.warn('No data to export');
      return;
    }

    // Extract headers
    const headers = Object.keys(data[0]);
    const csvContent = [
      headers.join(','),
      ...data.map(row => headers.map(fieldName => {
        const value = row[fieldName] === null || row[fieldName] === undefined ? '' : String(row[fieldName]);
        // Escape quotes and wrap in quotes if value contains comma
        const escapedValue = value.replace(/"/g, '""');
        return `"${escapedValue}"`;
      }).join(','))
    ].join('\n');

    this.downloadFile(csvContent, `${filename}.csv`, 'text/csv;charset=utf-8;');
  }

  private downloadFile(content: string, filename: string, contentType: string): void {
    const blob = new Blob(['\uFEFF' + content], { type: contentType });
    const url = URL.createObjectURL(blob);
    
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', filename);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }
}

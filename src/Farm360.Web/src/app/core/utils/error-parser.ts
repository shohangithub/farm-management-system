import { readStoredLanguage, translate } from '../i18n/i18n.service';

/** Generic ProblemDetails titles (English and the server's Bangla) that say nothing on their own. */
const GENERIC_TITLES = new Set(['Validation Failed', 'Bad Request', 'তথ্যে ভুল আছে', 'অনুরোধটি সঠিক নয়']);

/**
 * "Field: message" in English. In Bangla the server already names the field inside the message,
 * and the dictionary key is an English property name, so only the message is shown.
 */
function fieldMessage(field: string, message: string): string {
  return readStoredLanguage() === 'bn' ? message : `${field}: ${message}`;
}

/**
 * Utility to parse all types of API validation & exception responses from ASP.NET Core RFC 7807 ProblemDetails
 * or standard HTTP errors into a clean, human-readable string for display in UI dialogs and forms.
 */
export function parseApiError(err: any, fallbackMessage = 'An unexpected error occurred. Please try again.'): string {
  if (!err) return translate(fallbackMessage);

  const errorObj = err.error || err;

  // 1.5 Handle JSON string
  if (typeof errorObj === 'string' && (errorObj.trim().startsWith('{') || errorObj.trim().startsWith('['))) {
    try {
      const parsed = JSON.parse(errorObj);
      if (parsed?.detail && typeof parsed.detail === 'string') return parsed.detail;
      if (parsed?.message && typeof parsed.message === 'string') return parsed.message;
      if (parsed?.title && typeof parsed.title === 'string' && !GENERIC_TITLES.has(parsed.title)) return parsed.title;
    } catch {
      // not valid JSON, proceed
    }
  }

  // 1. Handle ASP.NET Core / FluentValidation errors dictionary: { errors: { FieldName: ["Msg1", "Msg2"] } }
  if (errorObj?.errors && typeof errorObj.errors === 'object') {
    const errorEntries = Object.entries(errorObj.errors);
    if (errorEntries.length > 0) {
      const messages: string[] = [];
      for (const [field, msgs] of errorEntries) {
        if (Array.isArray(msgs)) {
          msgs.forEach(m => messages.push(fieldMessage(field, m)));
        } else if (typeof msgs === 'string') {
          messages.push(fieldMessage(field, msgs));
        }
      }
      if (messages.length > 0) {
        return messages.join('\n');
      }
    }
  }

  // 2. Handle ProblemDetails detail
  if (errorObj?.detail && typeof errorObj.detail === 'string') {
    return errorObj.detail;
  }

  // 3. Handle ProblemDetails title (if descriptive)
  if (errorObj?.title && typeof errorObj.title === 'string' && !GENERIC_TITLES.has(errorObj.title)) {
    return errorObj.title;
  }

  // 4. Handle message property
  if (errorObj?.message && typeof errorObj.message === 'string') {
    return errorObj.message;
  }

  // 5. Handle direct string error response
  if (typeof errorObj === 'string' && errorObj.trim().length > 0) {
    return errorObj;
  }

  // 6. Handle HTTP Status Text / Message
  if (err.message && typeof err.message === 'string') {
    return err.message;
  }

  return translate(fallbackMessage);
}

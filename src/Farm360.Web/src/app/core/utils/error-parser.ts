/**
 * Utility to parse all types of API validation & exception responses from ASP.NET Core RFC 7807 ProblemDetails
 * or standard HTTP errors into a clean, human-readable string for display in UI dialogs and forms.
 */
export function parseApiError(err: any, fallbackMessage = 'An unexpected error occurred. Please try again.'): string {
  if (!err) return fallbackMessage;

  const errorObj = err.error || err;

  // 1. Handle ASP.NET Core / FluentValidation errors dictionary: { errors: { FieldName: ["Msg1", "Msg2"] } }
  if (errorObj?.errors && typeof errorObj.errors === 'object') {
    const errorEntries = Object.entries(errorObj.errors);
    if (errorEntries.length > 0) {
      const messages: string[] = [];
      for (const [field, msgs] of errorEntries) {
        if (Array.isArray(msgs)) {
          msgs.forEach(m => messages.push(`${field}: ${m}`));
        } else if (typeof msgs === 'string') {
          messages.push(`${field}: ${msgs}`);
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
  if (errorObj?.title && typeof errorObj.title === 'string' && errorObj.title !== 'Validation Failed' && errorObj.title !== 'Bad Request') {
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

  return fallbackMessage;
}

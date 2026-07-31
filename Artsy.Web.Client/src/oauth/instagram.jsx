import { useEffect } from 'react';

export default function OAuthCallback() {
  useEffect(() => {
    const urlParams = new URLSearchParams(window.location.search);
    const code = urlParams.get('code');
    const state = urlParams.get('state');
    const error = urlParams.get('error');
    const errorReason = urlParams.get('error_reason');

    if (error) {
      window.opener.postMessage(
        { type: 'INSTAGRAM_AUTH_ERROR', message: errorReason || error },
        window.location.origin
      );
    } else if (code && state) {
      window.opener.postMessage(
        { type: 'INSTAGRAM_AUTH_SUCCESS', code, state },
        window.location.origin
      );
    } else {
      window.opener.postMessage(
        { type: 'INSTAGRAM_AUTH_ERROR', message: 'Missing code or state in callback' },
        window.location.origin
      );
    }

    window.close();
  }, []);

  return (
    <div className="flex items-center justify-center min-h-screen">
      <p className="text-gray-500">Closing...</p>
    </div>
  );
}

package app.memoa;

import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;
import android.widget.Toast;

import androidx.browser.customtabs.CustomTabsCallback;

/**
 * LauncherActivity di androidbrowserhelper senza il crash del QualityEnforcer: se Chrome segnala
 * che la TWA non rispetta i criteri di qualità (Digital Asset Links non verificati, pagina in errore,
 * dispositivo offline) l'app resta aperta e mostra il motivo, invece di chiudersi.
 */
public class LauncherActivity extends com.google.androidbrowserhelper.trusted.LauncherActivity {
    private static final String TAG = "Memoa";
    private static final String QUALITY_ENFORCEMENT_CRASH = "quality_enforcement.crash";

    @Override
    protected CustomTabsCallback getCustomTabsCallback() {
        return new CustomTabsCallback() {
            @Override
            public Bundle extraCallbackWithResult(String callbackName, Bundle args) {
                if (!QUALITY_ENFORCEMENT_CRASH.equals(callbackName)) {
                    return Bundle.EMPTY;
                }
                String reason = args.getString("crash_reason");
                Log.w(TAG, "Criteri di qualità della TWA non rispettati: " + reason);
                new Handler(Looper.getMainLooper()).post(() ->
                        Toast.makeText(getApplicationContext(), reason, Toast.LENGTH_LONG).show());
                Bundle result = new Bundle();
                result.putBoolean("success", true);
                return result;
            }
        };
    }
}

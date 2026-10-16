package com.quantdale.walkgame.health;

import android.app.Activity;
import android.os.Bundle;
import android.widget.TextView;

/** Privacy rationale Health Connect links to from its permission screen (PRODUCT_SPEC §7a). */
public final class HealthPermissionRationaleActivity extends Activity {
    @Override protected void onCreate(Bundle state) {
        super.onCreate(state);
        TextView text = new TextView(this);
        int pad = (int) (24 * getResources().getDisplayMetrics().density);
        text.setPadding(pad, pad, pad, pad);
        text.setTextSize(18f);
        text.setText("Simple Walk Game reads only your step counts from Health Connect, on this device. "
            + "Steps restore the game world. Nothing is uploaded, shared or sold, and you can turn access off at any time "
            + "in Health Connect. Turning it off never deletes your progress.");
        setContentView(text);
    }
}

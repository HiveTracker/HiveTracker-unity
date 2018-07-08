package com.mteys.uduinoserial;

import android.app.Activity;
import android.os.Bundle;

import android.app.Fragment;
import com.unity3d.player.UnityPlayer;


import android.content.BroadcastReceiver;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.ServiceConnection;
import android.os.Bundle;
import android.os.Handler;
import android.os.IBinder;
import android.os.Message;
import android.view.View;
import android.widget.Button;
import android.widget.EditText;
import android.widget.TextView;
import android.widget.Toast;

import android.app.PendingIntent;
import android.app.Service;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.hardware.usb.UsbDevice;
import android.hardware.usb.UsbDeviceConnection;
import android.hardware.usb.UsbManager;
import android.os.Binder;
import android.os.Handler;
import android.os.IBinder;

import com.felhr.usbserial.CDCSerialDevice;
import com.felhr.usbserial.UsbSerialDevice;
import com.felhr.usbserial.UsbSerialInterface;

import java.io.UnsupportedEncodingException;
import java.util.HashMap;
import java.util.Map;
import java.lang.ref.WeakReference;
import java.util.Set;

/**
 * Created by marct on 17-Oct-17.
 * https://github.com/mik3y/usb-serial-for-android
 * http://answers.unity3d.com/questions/862548/call-non-static-method-from-subclass-in-java-from.html
 * http://eppz.eu/blog/unity-android-plugin-tutorial-3/
 */


import android.app.PendingIntent;
import android.app.Service;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.hardware.usb.UsbDevice;
import android.hardware.usb.UsbDeviceConnection;
import android.hardware.usb.UsbManager;
import android.os.Binder;
import android.os.Handler;
import android.os.IBinder;

import com.felhr.usbserial.CDCSerialDevice;
import com.felhr.usbserial.UsbSerialDevice;
import com.felhr.usbserial.UsbSerialInterface;

import java.io.UnsupportedEncodingException;
import java.util.HashMap;
import java.util.Map;


public class Serial_Connection extends Fragment
{
    // Constants.
    public static final String TAG = "Serial_Connection";
    // Singleton instance.
    public static Serial_Connection instance;
    // Unity context.
    String gameObjectName;

    public static void start(String gameObjectName)
    {
        // Instantiate and add to Unity Player Activity.
        instance = new Serial_Connection();
        instance.gameObjectName = gameObjectName; // Store 'GameObject' reference
        UnityPlayer.currentActivity.getFragmentManager().beginTransaction().add(instance, Serial_Connection.TAG).commit();
    }

    //Debug in toast message
    public void Debug(String message) {
        Toast.makeText(this.getActivity().getBaseContext(), message, Toast.LENGTH_LONG).show();
    }

    @Override
    public void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        mHandler = new MyHandler(this);
        setRetainInstance(true); // Retain between configuration changes (like device rotation)
        DebugUnity("onCreate");
    }


    public void DebugUnity(String message) {
        UnityPlayer.UnitySendMessage(gameObjectName, "CallUnityEvent",message);
    }

    public void SendUnityArduinoMessage(String message) {
        UnityPlayer.UnitySendMessage(gameObjectName, "PluginMessageReceived",message);
    }

    public void findPorts() {
        if(usbService != null) {
             usbService.findPorts();
        }
    }

    public void ChangeBaudRate(int targetBaud) {
        // Debug("Change Baud rate Android"  + targetBaud);
        DebugUnity("Change Baud rate Android" + targetBaud);

        if(usbService != null) {
            usbService.SetBaudRate(targetBaud);
        }
    }

    public void writeToArduino(String m) {
        if(usbService != null) {
            usbService.write(m.getBytes());
        } else {
            DebugUnity("usbService is null");
        }
    }

    /*
     * Notifications from UsbService will be received here.
     */
    private final BroadcastReceiver mUsbReceiver = new BroadcastReceiver() {
        @Override
        public void onReceive(Context context, Intent intent) {
            inBuffer = ""; //On any action we reset the buffer
            UnityPlayer.UnitySendMessage(gameObjectName, "CallUnityEvent", intent.getAction());

            DebugUnity(intent.getAction());

            switch (intent.getAction()) {
                case UsbService.ACTION_USB_PERMISSION_GRANTED: // USB PERMISSION GRANTED
                    Toast.makeText(context, "USB Ready", Toast.LENGTH_SHORT).show();
                    break;
                case UsbService.ACTION_USB_PERMISSION_NOT_GRANTED: // USB PERMISSION NOT GRANTED
                    Toast.makeText(context, "USB Permission not granted", Toast.LENGTH_SHORT).show();
                    break;
                case UsbService.ACTION_NO_USB: // NO USB CONNECTED
                    Toast.makeText(context, "No USB connected", Toast.LENGTH_SHORT).show();
                    break;
                case UsbService.ACTION_USB_DISCONNECTED: // USB DISCONNECTED
                    Toast.makeText(context, "USB disconnected", Toast.LENGTH_SHORT).show();
                    UnityPlayer.UnitySendMessage(gameObjectName, "BoardDisconnected", "");
               break;
                case UsbService.ACTION_USB_NOT_SUPPORTED: // USB NOT SUPPORTED
                    Toast.makeText(context, "USB device not supported", Toast.LENGTH_SHORT).show();
                    break;
            }
        }
    };

    private UsbService usbService = null;
    private MyHandler mHandler;

    private final ServiceConnection usbConnection = new ServiceConnection() {
        @Override
        public void onServiceConnected(ComponentName arg0, IBinder arg1) {
            if(usbService == null ) {
                usbService = ((UsbService.UsbBinder) arg1).getService();
                usbService.setHandler(mHandler);
                DebugUnity("Service connected");
            }
         }

        @Override
        public void onServiceDisconnected(ComponentName arg0) {
            usbService = null;
        }
    };


    @Override
    public void onResume() {
        super.onResume();
        setFilters();  // Start listening notifications from UsbService
        startService(UsbService.class, usbConnection, null); // Start UsbService(if it was not started before) and Bind it
    }

    @Override
    public void onPause() {
        super.onPause();
        getActivity().unregisterReceiver(mUsbReceiver);
        getActivity().unbindService(usbConnection);
    }

    private void startService(Class<?> service, ServiceConnection serviceConnection, Bundle extras) {
        if (!UsbService.SERVICE_CONNECTED) {
            Intent startService = new Intent(this.getActivity(), service);
            if (extras != null && !extras.isEmpty()) {
                Set<String> keys = extras.keySet();
                for (String key : keys) {
                    String extra = extras.getString(key);
                    startService.putExtra(key, extra);
                }
            }
            getActivity().startService(startService);
        }
        Intent bindingIntent = new Intent(this.getActivity(), service);
        getActivity().bindService(bindingIntent, serviceConnection, Context.BIND_AUTO_CREATE);
    }

    private void setFilters() {
        IntentFilter filter = new IntentFilter();
        filter.addAction(UsbService.ACTION_USB_PERMISSION_GRANTED);
        filter.addAction(UsbService.ACTION_NO_USB);
        filter.addAction(UsbService.ACTION_USB_DISCONNECTED);
        filter.addAction(UsbService.ACTION_USB_NOT_SUPPORTED);
        filter.addAction(UsbService.ACTION_USB_PERMISSION_NOT_GRANTED);
        getActivity().registerReceiver(mUsbReceiver, filter);
    }

    String inBuffer = "";
    int maxBufferLength = 150;

     private void AddToInBuffer(String string) {

         DebugUnity("Add to int buffer " + string);

         inBuffer += string;
         String[] lines = inBuffer.split("\\r\\n|\\n|\\r");

         if(lines.length > 1) {
             for(int i=0;i <lines.length -1; i++ ) {
                 if(lines[i] != null && !lines[i].isEmpty())
                     SendUnityArduinoMessage(lines[i]);
             }
             inBuffer = "";
             inBuffer = lines[lines.length -1];
         } else {
             if(lines.length > 0  && inBuffer.endsWith("\n") || inBuffer.length() > maxBufferLength ) {
                 SendUnityArduinoMessage(lines[0]);
                 inBuffer = "";
             }
         }
    }

    /*
     * This handler will be passed to UsbService. Data received from serial port is displayed through this handler
     */
    private static class MyHandler extends Handler {
        private final WeakReference<Serial_Connection> mActivity;

        public MyHandler(Serial_Connection activity) {
            mActivity = new WeakReference<>(activity);
        }

        @Override
        public void handleMessage(Message msg) {
            String parsedMessage = (String)msg.obj;

            if(parsedMessage.length() > 0 && (parsedMessage != null && parsedMessage != "")) {
                mActivity.get().AddToInBuffer(parsedMessage);
            }

            switch (msg.what) {
                case UsbService.MESSAGE_FROM_SERIAL_PORT:
                    break;
                case UsbService.CTS_CHANGE:
                   // Toast.makeText(mActivity.get(), "CTS_CHANGE",Toast.LENGTH_LONG).show();
                    break;
                case UsbService.DSR_CHANGE:
                  //  Toast.makeText(mActivity.get(), "DSR_CHANGE",Toast.LENGTH_LONG).show();
                    break;
            }
        }
    }
}

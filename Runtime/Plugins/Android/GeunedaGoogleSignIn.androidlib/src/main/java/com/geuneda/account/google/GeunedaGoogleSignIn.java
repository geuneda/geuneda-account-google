package com.geuneda.account.google;

import android.app.Activity;
import android.os.CancellationSignal;
import android.os.Handler;
import android.os.Looper;

import androidx.credentials.ClearCredentialStateRequest;
import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.ClearCredentialException;
import androidx.credentials.exceptions.GetCredentialCancellationException;
import androidx.credentials.exceptions.GetCredentialException;

import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;

import java.util.concurrent.Executor;

/**
 * Geuneda.Account.Google 의 Android 구현(GoogleAndroidSignIn.cs). Credential Manager 의 "Google 로 로그인" 화면으로 구글 id 토큰을 받는다.
 * 결과는 UI 스레드에서 콜백으로 넘기고, C# 이 Unity 메인 스레드로 옮긴다.
 */
public final class GeunedaGoogleSignIn {
    private static final int SUCCEEDED = 0;
    private static final int CANCELLED = 1;
    private static final int FAILED = 2;

    private GeunedaGoogleSignIn() {
    }

    /** 로그인을 시작하고 취소용 신호를 돌려준다. nonce 는 서버가 발급한 원문이고 id 토큰의 nonce 클레임에 그대로 들어간다. */
    public static CancellationSignal signIn(final Activity activity, String serverClientId, String nonce, final GeunedaGoogleSignInCallback callback) {
        final CancellationSignal signal = new CancellationSignal();
        final GetCredentialRequest request = new GetCredentialRequest.Builder()
                .addCredentialOption(new GetSignInWithGoogleOption.Builder(serverClientId).setNonce(nonce).build())
                .build();

        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    CredentialManager.create(activity).getCredentialAsync(activity, request, signal, mainExecutor(),
                            new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                                @Override
                                public void onResult(GetCredentialResponse response) {
                                    deliver(response.getCredential(), callback);
                                }

                                @Override
                                public void onError(GetCredentialException e) {
                                    int status = e instanceof GetCredentialCancellationException ? CANCELLED : FAILED;
                                    // 클래스 이름은 R8 이 바꾸므로 예외 종류는 getType() 으로 남긴다
                                    callback.onResult(status, null, e.getType() + ": " + e.getMessage());
                                }
                            });
                } catch (RuntimeException e) {
                    callback.onResult(FAILED, null, e.toString());
                }
            }
        });

        return signal;
    }

    /** Credential Manager 의 로그인 상태를 지운다(다음 로그인에서 계정을 다시 고르게). 끝나면 콜백한다(0 성공, 2 실패). */
    public static void signOut(final Activity activity, final GeunedaGoogleSignInCallback callback) {
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    CredentialManager.create(activity).clearCredentialStateAsync(new ClearCredentialStateRequest(), null, mainExecutor(),
                            new CredentialManagerCallback<Void, ClearCredentialException>() {
                                @Override
                                public void onResult(Void result) {
                                    callback.onResult(SUCCEEDED, null, null);
                                }

                                @Override
                                public void onError(ClearCredentialException e) {
                                    callback.onResult(FAILED, null, e.getType() + ": " + e.getMessage());
                                }
                            });
                } catch (RuntimeException e) {
                    callback.onResult(FAILED, null, e.toString());
                }
            }
        });
    }

    private static void deliver(Credential credential, GeunedaGoogleSignInCallback callback) {
        if (!(credential instanceof CustomCredential) || !GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(credential.getType())) {
            callback.onResult(FAILED, null, "Unexpected credential type " + credential.getType());
            return;
        }

        try {
            callback.onResult(SUCCEEDED, GoogleIdTokenCredential.createFrom(credential.getData()).getIdToken(), null);
        } catch (RuntimeException e) {
            callback.onResult(FAILED, null, e.toString());
        }
    }

    private static Executor mainExecutor() {
        final Handler handler = new Handler(Looper.getMainLooper());
        return new Executor() {
            @Override
            public void execute(Runnable command) {
                handler.post(command);
            }
        };
    }
}

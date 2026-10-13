package com.geuneda.account.google;

/** 로그인·로그아웃 결과를 C#(AndroidJavaProxy)으로 넘긴다. status 는 GoogleSignInStatus 와 같다(0 성공, 1 취소, 2 실패). */
public interface GeunedaGoogleSignInCallback {
    void onResult(int status, String value, String message);
}

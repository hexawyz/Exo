#include "pch.h"
#include <devquery.h>

typedef struct _ContextWrapper
{
    PDEV_QUERY_RESULT_CALLBACK Callback;
    PVOID Context;
} ContextWrapper;

// It seems we need to go through (useless) hoops in order to use DevQuery from C#, as the DevQuery API will ensure that the callback is tied to a HMODULE.
// The function here will only serve as a call forwarder to redirect the data to our C# code. I really wish we could do without this, but it seems complicated as-is.
__declspec(dllexport) VOID WINAPI DevQueryCallback(
    _In_ HDEVQUERY hDevQuery,
    _In_opt_ PVOID pContext,
    _In_ const DEV_QUERY_RESULT_ACTION_DATA* pActionData)
{
    if (pContext == NULL) return;

    ContextWrapper* ctx = (ContextWrapper*)pContext;

    ctx->Callback(hDevQuery, ctx->Context, pActionData);
}

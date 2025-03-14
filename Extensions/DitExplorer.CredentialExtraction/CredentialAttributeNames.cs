using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.CredentialExtraction;
internal static class CredentialAttributeNames
{
	internal const string DbcsPwd = "dBCSPwd";
	internal const string UnicodePwd = "unicodePwd";
	internal const string LmPwdHistory = "lmPwdHistory";
	internal const string NtPwdHistory = "ntPwdHistory";
	internal const string SupplementalCredentials = "supplementalCredentials";
	internal const string UserPrincipalName = "userPrincipalName";
	internal const string SamAccountName = "samAccountName";
	internal const string ObjectSid = "objectSid";
	internal const string PekList = "pekList";
}

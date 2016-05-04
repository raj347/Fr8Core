﻿using System.Linq;
using Fr8Data.Constants;
using Fr8Data.Crates;
using Fr8Data.DataTransferObjects;
using Fr8Data.DataTransferObjects.Helpers;
using Fr8Data.Manifests;
using Hub.Managers;
using NUnit.Framework;

namespace UtilitiesTesting.Asserts
{
    public static partial class AssertEx
    {
        public static void AssertPayloadHasAuthenticationError(PayloadDTO payload)
        {
            var storage = new CrateManager().GetStorage(payload);
            var operationalStateCM = storage.CrateContentsOfType<OperationalStateCM>().Single();
            ErrorDTO errorMessage;
            operationalStateCM.CurrentActivityResponse.TryParseErrorDTO(out errorMessage);
            Assert.AreEqual(ActivityResponse.Error.ToString(), operationalStateCM.CurrentActivityResponse.Type);
            Assert.AreEqual(ActivityErrorCode.NO_AUTH_TOKEN_PROVIDED, operationalStateCM.CurrentActivityErrorCode);
            Assert.AreEqual("No AuthToken provided.", errorMessage.Message);
        }

        public static void AssertPayloadHasError(PayloadDTO payload)
        {
            var storage = new CrateManager().GetStorage(payload);
            var operationalStateCM = storage.CrateContentsOfType<OperationalStateCM>().Single();
            ErrorDTO errorMessage;
            Assert.IsTrue(operationalStateCM.CurrentActivityResponse.TryParseErrorDTO(out errorMessage));
        }
    }
}

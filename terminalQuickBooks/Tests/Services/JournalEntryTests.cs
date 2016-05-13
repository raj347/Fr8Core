﻿using System;
using System.Linq;
using Data.Entities;
using Fr8Data.DataTransferObjects;
using Intuit.Ipp.Data;
using NUnit.Framework;
using StructureMap;
using terminalQuickBooks.Interfaces;
using TerminalBase.Infrastructure;
using UtilitiesTesting;

namespace terminalQuickBooks.Tests.Services
{
    public class JournalEntryTests : BaseTest
    {
        private terminalQuickBooks.Services.JournalEntry _journalEntry;
        private IServiceWorker _serviceWorker;
        public override void SetUp()
        {
            base.SetUp();
            TerminalBootstrapper.ConfigureTest();
            _journalEntry = new terminalQuickBooks.Services.JournalEntry();
            _serviceWorker = ObjectFactory.GetInstance<IServiceWorker>();
        }
        
        [Test]
        public void JournalEntryService_ConvertsCrate_To_JouralEntry()
        {
            //Assign
            var curCrate = Fixtures.Fixtures.GetAccountingTransactionCM();
            var curTransactionDTO = curCrate.AccountingTransactions[0];
            //Act
            var journalEntry = _journalEntry.CreateQbJournalEntry(curTransactionDTO);
            //Assert First Line
            Assert.AreEqual("100",journalEntry.Line[0].Amount.ToString());
            Assert.AreEqual("1",journalEntry.Line[0].Id);
            var firstLineDetails = (JournalEntryLineDetail) journalEntry.Line[0].AnyIntuitObject;
            Assert.AreEqual("Account-A", firstLineDetails.AccountRef.name);
            Assert.AreEqual("Debit", firstLineDetails.PostingType.ToString());
            Assert.AreEqual("Move money to Accout-B", journalEntry.Line[0].Description);
            //Assert Second Line
            Assert.AreEqual("100", journalEntry.Line[1].Amount.ToString());
            Assert.AreEqual("2", journalEntry.Line[1].Id.ToString());
            var secondLineDetails = (JournalEntryLineDetail)journalEntry.Line[1].AnyIntuitObject;
            Assert.AreEqual("Account-B", secondLineDetails.AccountRef.name);
            Assert.AreEqual("Credit", secondLineDetails.PostingType.ToString());
            Assert.AreEqual("Move money from Accout-A", journalEntry.Line[1].Description);
            //Assert Journal Entry Data
            Assert.AreEqual("Code1", journalEntry.DocNumber);
            Assert.AreEqual(DateTime.Parse("2015-12-15"), journalEntry.TxnDate);
            Assert.AreEqual("That is the test crate", journalEntry.PrivateNote);
        }

        [Test]
        public void JournalEntryService_ConvertJournalEntry_To_Crate()
        {
            //Assign
            var curJournalEntry = Fixtures.Fixtures.CreateJournalEntry();
            //Act
            var curTransactionDTO = _journalEntry.GetAccountingTransactionData(curJournalEntry);
            //Assert General Data
            Assert.AreEqual("DocNu1", curTransactionDTO.Name);
            Assert.AreEqual(DateTime.UtcNow.Date, curTransactionDTO.TransactionDate);
            Assert.AreEqual("This is the test Journal Entry", curTransactionDTO.Memo);
            //Assert First Line
            var curFirstLine = curTransactionDTO.FinancialLines[0];
            Assert.AreEqual("36", curFirstLine.AccountId);
            Assert.AreEqual("Accumulated Depreciation", curFirstLine.AccountName);
            Assert.AreEqual("100", curFirstLine.Amount);
            Assert.AreEqual(PostingTypeEnum.Debit.ToString(), curFirstLine.DebitOrCredit);
            Assert.AreEqual("That is the first line description", curFirstLine.Description);
            //Assert Second Line
            var curSecondLine = curTransactionDTO.FinancialLines[1];
            Assert.AreEqual("36", curSecondLine.AccountId);
            Assert.AreEqual("Accumulated Depreciation", curSecondLine.AccountName);
            Assert.AreEqual("100", curSecondLine.Amount);
            Assert.AreEqual(PostingTypeEnum.Credit.ToString(), curSecondLine.DebitOrCredit);
            Assert.AreEqual("That is the second line description", curSecondLine.Description);
        }

        /// <summary>
        /// Method is created for testing purposes
        /// It takes StandardAccountingTransactionDTO as an input, converts it into journal entry, looks for similar journal entries in the Sandbox,
        /// takes first occurance from the list, and returns converted back crate object
        /// </summary>
        /// <param name="StandardAccountingTransactionDTO"></param>
        /// <param name="authTokenDO"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public StandardAccountingTransactionDTO Find(
            StandardAccountingTransactionDTO curAccountingTransactionDto, 
            AuthorizationTokenDO authTokenDO,
            string userId)
        {
            var curJournalEntry = _journalEntry.CreateQbJournalEntry(curAccountingTransactionDto);
            var curDataService = _serviceWorker.GetDataService(authTokenDO, userId);
            Intuit.Ipp.Data.JournalEntry resultJournalEntry;
            try
            {
                resultJournalEntry = curDataService.FindAll(curJournalEntry).ToList().First();
                return _journalEntry.GetAccountingTransactionData(resultJournalEntry);
            }
            catch (Exception curException)
            {
                throw curException;
            }
        }
    }
}
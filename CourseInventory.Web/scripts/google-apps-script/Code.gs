const ADMIN_EMAIL = 'oriangidolcalebou@gmail.com';
const SUPPORT_FOLDER_PATH = 'InventoryStudio/SupportTickets';

function checkSupportTickets() {
  const folder = findFolderByPath(SUPPORT_FOLDER_PATH);

  if (!folder) {
    console.error(`Support ticket folder not found: ${SUPPORT_FOLDER_PATH}`);
    return;
  }

  console.log(`Checking support tickets in folder: ${SUPPORT_FOLDER_PATH}`);

  const files = folder.getFiles();

  while (files.hasNext()) {
    const file = files.next();
    const fileName = file.getName();

    if (fileName.startsWith('PROCESSED_') || fileName.startsWith('ERROR_')) {
      console.log(`Skipping already handled file: ${fileName}`);
      continue;
    }

    if (!fileName.toLowerCase().endsWith('.json')) {
      console.log(`Skipping non-JSON file: ${fileName}`);
      continue;
    }

    try {
      const content = file.getBlob().getDataAsString('UTF-8');
      const ticket = JSON.parse(content);

      const priority = ticket.priority || 'Unknown';
      const subject = `[Inventory Studio] Support ticket - ${priority}`;
      const body = formatTicketEmail(ticket, fileName);

      MailApp.sendEmail({
        to: ADMIN_EMAIL,
        subject: subject,
        body: body
      });

      file.setName(`PROCESSED_${fileName}`);
      console.log(`Processed support ticket: ${fileName}`);
    } catch (error) {
      console.error(`Failed to process support ticket file ${fileName}: ${error}`);

      try {
        file.setName(`ERROR_${fileName}`);
        console.log(`Renamed invalid JSON file to ERROR_${fileName}`);
      } catch (renameError) {
        console.error(`Could not rename invalid file ${fileName}: ${renameError}`);
      }
    }
  }

  console.log('Support ticket check finished.');
}

function formatTicketEmail(ticket, fileName) {
  return [
    'Inventory Studio support ticket',
    '',
    `Reported by: ${ticket.reportedBy || '(missing)'}`,
    `Reporter email: ${ticket.reportedByEmail || '(missing)'}`,
    `Inventory name: ${ticket.inventory || '(missing)'}`,
    `Priority: ${ticket.priority || '(missing)'}`,
    `Created at UTC: ${ticket.createdAtUtc || '(missing)'}`,
    `Inventory link: ${ticket.link || '(missing)'}`,
    '',
    'Summary:',
    ticket.summary || '(missing)',
    '',
    `Original file name: ${fileName}`
  ].join('\n');
}

function findFolderByPath(path) {
  const parts = path.split('/').filter(Boolean);

  if (parts.length === 0) {
    return null;
  }

  const rootFolders = DriveApp.getFoldersByName(parts[0]);

  while (rootFolders.hasNext()) {
    const folder = findChildFolderByPathParts(rootFolders.next(), parts, 1);

    if (folder) {
      return folder;
    }
  }

  return null;
}

function findChildFolderByPathParts(currentFolder, parts, index) {
  if (index >= parts.length) {
    return currentFolder;
  }

  const childFolders = currentFolder.getFoldersByName(parts[index]);

  while (childFolders.hasNext()) {
    const folder = findChildFolderByPathParts(childFolders.next(), parts, index + 1);

    if (folder) {
      return folder;
    }
  }

  return null;
}

/*
Time-driven trigger setup, every 5 minutes:
1. Open the Apps Script project.
2. Click Triggers in the left sidebar.
3. Click Add Trigger.
4. Choose function: checkSupportTickets.
5. Choose event source: Time-driven.
6. Choose type: Minutes timer.
7. Choose interval: Every 5 minutes.
8. Save and authorize DriveApp and MailApp permissions.

Manual test:
1. Open Apps Script.
2. Select checkSupportTickets.
3. Click Run.
4. Authorize permissions.
5. Check Gmail.
6. Confirm one email is sent.
7. Confirm the processed JSON file is renamed with PROCESSED_.
*/
